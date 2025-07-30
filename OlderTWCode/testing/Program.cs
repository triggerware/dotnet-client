// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Net;
using TWClients;
using TWClients.JsonRpcMessages;

var client = new TriggerwareClient("narg", IPAddress.Loopback, 5221);

// testing regular query
var statement1 = new QueryStatement<double[]>(client);
var exhaustedQuery = statement1.ExecuteQueryAsync("((x) s.t. (inflation 1995 1991 x))", "fol", "AP5").Result;
exhaustedQuery.Next();
Console.WriteLine(exhaustedQuery.Get()[0]);

// testing prepared queries
var statement2 = new PreparedQuery<double[]>("((x) s.t. (inflation 1995 1991 x))", "fol", "AP5", client);
var query = statement2.ExecuteQuery();
query.Next();

var x = 4;



Console.WriteLine( query.Get()[0] );
