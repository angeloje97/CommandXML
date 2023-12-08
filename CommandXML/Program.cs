// See https://aka.ms/new-console-template for more information
using CommandXML;
using System.Text.RegularExpressions;

Console.WriteLine("Hello, World!");


var count = 5;

//for(int i = 0; i < count; i = (i + 1) % count)
//{
//    Console.WriteLine(i);
//    Thread.Sleep(1000);
//}

//CommandController.Initiate();

//while (CommandController.reading) await Task.Delay(1000);
string generatedRegexPattern(string[] items)
{
    string pattern = string.Join(".*", items);

    return ".*" + pattern + ".*";
}


var items = new string[] { "quick", "jumps", "dog" };
var pattern = generatedRegexPattern(items);

var expected = "The quick brown fox jumps over the lazy dog";

Console.WriteLine(Regex.IsMatch(expected, pattern));

Console.ReadLine();