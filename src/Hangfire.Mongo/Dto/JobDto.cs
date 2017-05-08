﻿using System;
using System.Collections.Generic;
using Hangfire.Mongo.MongoUtils;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Hangfire.Mongo.Dto
{
#pragma warning disable 1591
    public class JobDto
    {
        [BsonId(IdGenerator = typeof(AutoIncrementIntIdGenerator))]
        public int Id { get; set; }
        
        public string StateName { get; set; }

        public string InvocationData { get; set; }

        public string Arguments { get; set; }

        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

        public StateDto[] StateHistory { get; set; } = new StateDto[0];

        public DateTime CreatedAt { get; set; }

        public DateTime? ExpireAt { get; set; }
    } 
#pragma warning restore 1591
}