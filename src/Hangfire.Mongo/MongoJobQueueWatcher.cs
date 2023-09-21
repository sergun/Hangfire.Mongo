using System.Threading;
using Hangfire.Logging;
using Hangfire.Mongo.Database;
using Hangfire.Mongo.Dto;
using Hangfire.Server;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Hangfire.Mongo
{
    /// <summary>
    /// uses mongo feature watch to observe locks and added jobs
    /// </summary>
    public class MongoJobQueueWatcher : IBackgroundProcess, IServerComponent
    {
        /// <summary>
        /// Logger instance
        /// </summary>
        protected static readonly ILog Logger = LogProvider.For<MongoJobQueueWatcher>();
        /// <summary>
        /// DbContext
        /// </summary>
        protected readonly HangfireDbContext DbContext;
        /// <summary>
        /// StorageOptions
        /// </summary>
        protected readonly MongoStorageOptions StorageOptions;
        /// <summary>
        /// JobQueue semaphore
        /// </summary>
        protected readonly IJobQueueSemaphore JobQueueSemaphore;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="dbContext"></param>
        /// <param name="storageOptions"></param>
        /// <param name="jobQueueSemaphore"></param>
        public MongoJobQueueWatcher(
            HangfireDbContext dbContext,
            MongoStorageOptions storageOptions,
            IJobQueueSemaphore jobQueueSemaphore)
        {
            DbContext = dbContext;
            StorageOptions = storageOptions;
            JobQueueSemaphore = jobQueueSemaphore;

        }
        /// <inheritdoc />
        public virtual void Execute(CancellationToken cancellationToken)
        {
            var pipeline = new[]
            {
                new BsonDocument
                {
                    ["$match"] = new BsonDocument
                    {
                        ["operationType"] =  "update",
                        [$"updateDescription.updatedFields.{nameof(JobDto.Queue)}"] = new BsonDocument
                        {
                            ["$exists"] = true
                        },
                        [$"updateDescription.updatedFields.{nameof(JobDto.Queue)}"] = new BsonDocument
                        {
                            ["$ne"] = BsonNull.Value
                        }
                    }
                }
            };

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var cursor = DbContext
                    .Database
                    .GetCollection<BsonDocument>(DbContext.JobGraph.CollectionNamespace.CollectionName)
                    .Watch<BsonDocument>(pipeline);
                    
                    if (Logger.IsTraceEnabled())
                    {
                        Logger.Trace("Watcher: Watching for enqueued jobs");
                    }

                    foreach (var change in cursor.ToEnumerable(cancellationToken))
                    {
                        var queue = change["updateDescription"]["updatedFields"][nameof(JobDto.Queue)].AsString;
                        JobQueueSemaphore.Release(queue);
                        if (Logger.IsTraceEnabled())
                        {
                            Logger.Trace("Watcher: Job enqueued, queue: " + queue);
                        }
                    }
                }
                catch (MongoCommandException e)
                {
                    if (e.Message.Contains("$changeStream stage is only supported on replica sets"))
                    {
                        Logger.ErrorException(
                            "Current db does not support change stream (not a replica set, https://docs.mongodb.com/manual/reference/method/db.collection.watch/)\r\n" +
                                    "if you need instant (almost) handling of enqueued jobs, please set 'CheckQueuedJobsStrategy' to 'TailNotificationsCollection' in MongoStorageOptions", e);
                        throw;
                    }
                    // wait max allowed
                    cancellationToken.WaitHandle.WaitOne(MongoNotificationObserver.MaxTimeout);
                }
                
            }
        }

        /// <inheritdoc />
        public void Execute(BackgroundProcessContext context)
        {
            Execute(context.StoppingToken);
        }
    }
}