﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hangfire.Mongo.Database;
using Hangfire.Mongo.Dto;
using MongoDB.Driver;

namespace Hangfire.Mongo.Migration
{

    /// <summary>
    /// Class for running a full migration
    /// </summary>
    internal class MongoMigrationRunner
    {
        private HangfireDbContext _dbContext;
        private MongoStorageOptions _storageOptions;

        public MongoMigrationRunner(HangfireDbContext dbContext, MongoStorageOptions storageOptions)
        {
            _dbContext = dbContext;
            _storageOptions = storageOptions;
        }


        /// <summary>
        /// Executes all migration steps between the given shemas.
        /// </summary>
        /// <param name="fromSchema">Spcifies the current shema in the database. Migration steps targeting this schema will not be executed.</param>
        /// <param name="toSchema">Specifies the schema to migrate the database to. On success this will be the schema for the database.</param>
        public void Execute(MongoSchema fromSchema, MongoSchema toSchema)
        {
            if (fromSchema == toSchema)
            {
                // Nothing to migrate - let's just get outa here
                return;
            }

            if (fromSchema > toSchema)
            {
                throw new InvalidOperationException($@"The {nameof(fromSchema)} ({fromSchema}) cannot be larger than {nameof(toSchema)} ({toSchema})");
            }

            var migrationSteps = LoadMigrationSteps()
                .Where(step => step.TargetSchema > fromSchema && step.TargetSchema <= toSchema)
                .GroupBy(step => step.TargetSchema);

            foreach (var migrationGroup in migrationSteps)
            {
                foreach (var migrationStep in migrationGroup)
                {
                    if (!migrationStep.Execute(_dbContext.Database, _storageOptions))
                    {
                        throw new MongoMigrationException(migrationStep);
                    }
                }
				_dbContext.Schema.DeleteMany(_ => true);
				_dbContext.Schema.InsertOne(new SchemaDto { Version = (int)migrationGroup.Key });
			}
        }


        /// <summary>
        /// Loads, instantiates and orders the migration steps available in this assembly.
        /// </summary>
        private IEnumerable<IMongoMigrationStep> LoadMigrationSteps()
        {
            var types = GetType().GetTypeInfo().Assembly.GetTypes();
            return types
                .Where(t => !t.GetTypeInfo().IsAbstract && t.GetTypeInfo().GetInterfaces().Contains(typeof(IMongoMigrationStep)))
                .Select(t => (IMongoMigrationStep)Activator.CreateInstance(t))
                .OrderBy(step => (int)step.TargetSchema).ThenBy(step => step.Sequence);
        }

    }
}
