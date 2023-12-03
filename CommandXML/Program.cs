// See https://aka.ms/new-console-template for more information
using CommandXML;

Console.WriteLine("Hello, World!");


var count = 5;

for(int i = 0; i < count; i = (i + 1) % count)
{
    Console.WriteLine(i);
    Thread.Sleep(1000);
}

//CommandController.Initiate();

//while (CommandController.reading) await Task.Delay(1000);