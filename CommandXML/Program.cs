// See https://aka.ms/new-console-template for more information
using CommandXML;

Console.WriteLine("Hello, World!");


CommandController.Initiate();

while (CommandController.reading) await Task.Delay(1000);