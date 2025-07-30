// See https://aka.ms/new-console-template for more information

using System.Net;
using Triggerware;

var tw = new TriggerwareClient(IPAddress.Loopback, 5221);
tw.Start();

var relData = tw.GetRelData();
var other = relData.SelectMany(e => e.Elements).ToArray();
foreach (var o in other)
{
    Console.WriteLine(o.Name);
}

return;

// subscriptions
var alerter = new EventPrinter(tw, "NEGATIVE-TWEET;");
AddNegativeTweet("2024-11-07", "3:00:00", "asdf", "politics");
AddNegativeTweet("2024-11-07", "3:05:00", "asdf", "politics");


// view
var view = new View<object?[]>(tw, new FolQuery("((x) s.t. (= 11 x))"));
var set1 = view.Execute();
foreach (var item in set1)
foreach (var thing in item)
    Console.WriteLine(thing);

// queries
var prepared = new PreparedQuery<object?[]>(tw, new FolQuery("((x) s.t. (= y x))"));
prepared.SetParameter("Y", "aoeu");
var set2 = prepared.Execute();

foreach (var item in set2)
foreach (var thing in item)
    Console.WriteLine(thing);

return;

void AddNegativeTweet(string date, string time, string message, string topic)
{
    var parameters = new Dictionary<string, object>
    {
        { "date", date },
        { "time", time },
        { "message", message },
        { "topic", topic }
    };

    tw.Call<object?>("add-negative-tweet", parameters);
}

internal class EventPrinter(TriggerwareClient client, string condition)
    : Subscription<object>(client, new FolQuery(condition))
{
    public override void HandleNotification(object tuple)
    {
        Console.WriteLine(tuple);
    }
}