/*
 * MIT License
 *
 * Copyright (c) 2019 LambdaSharp
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Challenge.LambdaRobots.Common;
using Newtonsoft.Json;

namespace Challenge.LambdaRobots.Server.Common {

    public class GameRecord : IGameTableSingletonRecord {

        //--- Properties ---
        public string PK { get; set; }
        public string SK => "GAME";
        public Game Game { get; set; }
    }

    public class GameSessionRecord : IGameTableMultiRecord {

        //--- Properties ---
        public string PK { get; set; }
        public string SK => $"{SKPrefix}{ConnectionId}";
        public string ConnectionId { get; set; }
        public string SKPrefix => "connection-";
    }

    public class GameStateMachineRecord : IGameTableSingletonRecord {

        //--- Properties ---
        public string PK { get; set; }
        public string SK => "STATE-MACHINE";
        public string StateMachineArn { get; set; }
    }

    public interface IGameTableSingletonRecord {

        //--- Properties ---
        string SK { get; }
    }

    public interface IGameTableMultiRecord {

        //--- Properties ---
        string SKPrefix { get; }
    }

    public class GameTable {

        //--- Fields ---
        private readonly IAmazonDynamoDB _dynamoDbClient;
        private readonly Table _table;

        //--- Constructors ---
        public GameTable(string tableName, IAmazonDynamoDB dynamoDbClient) {
            _dynamoDbClient = dynamoDbClient ?? throw new ArgumentNullException(nameof(dynamoDbClient));
            _table = Table.LoadTable(_dynamoDbClient, tableName ?? throw new ArgumentNullException(nameof(tableName)));
        }

        //--- Methods ---
        public Task PutAsync<T>(T record) => _table.PutItemAsync(Document.FromJson(JsonConvert.SerializeObject(record)));

        public Task<T> GetAsync<T>(string pk) where T : IGameTableSingletonRecord, new()
            => GetAsync<T>(pk, new T().SK);

        public async Task<T> GetAsync<T>(string pk, string sk) {
            var record = await _table.GetItemAsync(new Dictionary<string, DynamoDBEntry> {
                ["PK"] = pk,
                ["SK"] = sk
            });
            return (record != null)
                ? JsonConvert.DeserializeObject<T>(record.ToJson())
                : default;
        }

        public Task DeleteAsync<T>(string pk) where T : IGameTableSingletonRecord, new()
            => DeleteAsync(pk, new T().SK);

        public Task DeleteAsync(string pk, string sk)
            => _table.DeleteItemAsync(pk, sk);

        public async Task<IEnumerable<T>> GetAllAsync<T>(string pk) where T : IGameTableMultiRecord, new() {
            var records = await _table.Query(pk, new Expression {
                ExpressionStatement = "begins_with(SK, :sortkeyprefix)",
                ExpressionAttributeValues = {
                    [":sortkeyprefix"] = new T().SKPrefix
                }
            }).GetRemainingAsync();
            return records.Select(record => JsonConvert.DeserializeObject<T>(record.ToJson())).ToList();
        }
    }
}