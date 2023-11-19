using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace CommandXML
{

    public class CommandItem
    {
        public string description;
        public Action action;

        public CommandItem(string description, Action action)
        {
            this.description = description;
            this.action = action;
        }
    }
    internal class CommandController
    {
        static CommandController instance;

        static readonly string path = "C:\\WorkSpace\\Porgramming\\Sandbox";
        static readonly string fileName = "Commands.xml";

        public static bool reading;


        public static void Initiate()
        {
            if (instance != null) return;
            instance = new();


            instance.ReadData();
        }

        public CommandController()
        {
            XmlDocument currentDoc = new XmlDocument();
            var root = currentDoc.CreateElement("CommandController");
            var commandElement = currentDoc.CreateElement("Command");

            //Create Comments
            root.AppendChild(currentDoc.CreateComment("List of Commands"));
            foreach(KeyValuePair<string, CommandItem> command in commands)
            {
                root.AppendChild(currentDoc.CreateComment($"{command.Key}: {command.Value.description}"));
            }


            root.AppendChild(commandElement);
            currentDoc.AppendChild(root);


            XmlWriterSettings settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
                NewLineChars = "\r\n",
            };

            XmlWriter writer = XmlWriter.Create(Path.Combine(path, "output.xml"), settings);


            currentDoc.Save(writer);
            currentDoc.Save(Path.Combine(path, fileName));
        }

        public async void ReadData()
        {
            reading = true;
            var doc = new XmlDocument();

            while (reading)
            {

                await Task.Delay(1000);
                doc.Load(Path.Combine(path, fileName));

                var commandNodes = doc.GetElementsByTagName("Command");
                if (commandNodes.Count == 0) continue;

                foreach(XmlElement commandNode in commandNodes)
                {
                    var commandSent = commandNode.GetAttribute("name");
                    Console.WriteLine($"Inner text of the <Command> element: {commandSent}");
                    Console.WriteLine(commandSent); 
                    HandleCommand(commandSent);

                }

                ClearCommands();
                
            }

            reading = false;
        }

        public void HandleCommand(string commandString)
        {
            if (!commands.ContainsKey(commandString)) return;
            Console.WriteLine("Running Command");
            commands[commandString].action();
        }

        public void ClearCommands() 
        {
            var doc = new XmlDocument();
            doc.Load(Path.Combine(path, fileName));

            var dataNodes = doc.GetElementsByTagName("Command");
            
            foreach(XmlElement element in dataNodes)
            {
                element.SetAttribute("name", "");
            }

            doc.Save(Path.Combine(path, fileName));
        }

        public static readonly Dictionary<string, CommandItem> commands = new() {
            { "SayHello", new(
                    "Prints Hello World",
                    () => {
                        Console.WriteLine("Hello World");
                    }
                ) },
            { "End", new(
                    "Prints, ends the console",
                    () => {
                        reading = false;
                    }
                ) }
        };
    }
}
