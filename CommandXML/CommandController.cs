using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace CommandXML
{

    public class CommandItem
    {
        public string description;
        public Action<XmlElement> action;

        public CommandItem(string description, Action<XmlElement> action)
        {
            this.description = description;
            this.action = action;
        }

        public CommandItem(string description, Action action)
        {
            this.description = description;
            this.action += (element) => { action(); };
        }
    }
    internal class CommandController
    {
        static CommandController instance;

        static readonly string path = "C:\\WorkSpace\\Porgramming\\Sandbox";
        static readonly string fileName = "Commands.xml";

        public static bool reading;


        List<string> commandLogs;
        readonly int maxLogs = 25;
        readonly string emptyLog = "|                                                                            |";
        XmlDocument doc;


        public static async void Initiate()
        {
            if (instance != null) return;
            instance = new();


            await instance.ReadData();
            instance.CleanUp();
        }

        public CommandController()
        {
            doc = new XmlDocument();
            var root = doc.CreateElement("CommandXML");

            commandLogs = new();

            doc.AppendChild(root);


            CreateConsole();
            CreateControls();

            SaveDoc();

            void CreateConsole()
            {
                var console = doc.CreateElement("Console");

                for (int i = 0; i < maxLogs; i++)
                {
                    console.AppendChild(doc.CreateComment(emptyLog));
                }

                root.AppendChild(console);

            }

            void CreateControls()
            {
                var commandElement = doc.CreateElement("Command");
                var controls = doc.CreateElement("Controls");


                root.AppendChild(controls);
                controls.AppendChild(commandElement);
                commandElement.SetAttribute("name", "");

                controls.AppendChild(doc.CreateComment("List of Commands"));
                foreach (KeyValuePair<string, CommandItem> command in commands)
                {
                    controls.AppendChild(doc.CreateComment($"{command.Key}: {command.Value.description}"));
                }
            }
        }

        async Task  ReadData()
        {
            reading = true;

            while (reading)
            {

                await Task.Delay(1000);
                doc.Load(Path.Combine(path, fileName));

                var commandNodes = doc.GetElementsByTagName("Command");
                if (commandNodes.Count == 0) continue;

                bool hasInvoked = false;

                foreach(XmlElement commandNode in commandNodes)
                {
                    var commandSent = commandNode.GetAttribute("name");
                    if (commandSent.Equals("")) continue;
                    hasInvoked = true;
                    HandleCommand(commandNode);

                }

                if (hasInvoked)
                {
                    UpdateConsole();
                    ClearCommands();
                    SaveDoc();
                }
                
            }

            reading = false;
        }

        public void HandleCommand(XmlElement element)
        {
            var commandString = element.GetAttribute("name");
            if (!commands.ContainsKey(commandString)) {
                WriteLine($"Unknown Command: {commandString}");
                return;
            };

            WriteLine($"Running Command: {commandString}");
            commands[commandString].action(element);
        }

        public void WriteLine(string str)
        {
            commandLogs.Add(str);
        }

        public void ClearCommands() 
        {
            var dataNodes = doc.GetElementsByTagName("Command");
            
            foreach(XmlElement element in dataNodes)
            {
                element.SetAttribute("name", "");
            }
        }

        void UpdateConsole()
        {
            var consoles = doc.GetElementsByTagName("Console");

            foreach(XmlElement console in consoles)
            {
                console.RemoveAll();

                int leftOvers = maxLogs;
                for (int i = commandLogs.Count - 1; i >= 0; i--)
                {
                    var log = commandLogs[i];
                    console.AppendChild(doc.CreateComment(log));

                    leftOvers--;
                }

                while (leftOvers > 0)
                {
                    console.AppendChild(doc.CreateComment(emptyLog));

                    leftOvers--;
                }
            }

            

        }

        public void SaveDoc()
        {
            doc.Save(Path.Combine(path, fileName));
        }

        public void CleanUp()
        {
            if (!File.Exists(Path.Combine(path, fileName))) return;
            File.Delete(Path.Combine(path, fileName));
        }

        public static readonly Dictionary<string, CommandItem> commands = new() {
            { "SayHello", new(
                    "Prints Hello World",
                    (element) => {
                        Console.WriteLine("Hello World");
                    }
                ) },
            { "End", new(
                    "Prints, ends the console",
                    (element) => {
                        reading = false;
                    }
                ) }
        };

        public bool Validate(XmlElement elementWithAttributes, Dictionary<string, Type> pairs)
        {
            foreach(XmlAttribute attribute in elementWithAttributes)
            {
                if (pairs.ContainsKey(attribute.Name))
                {

                }
            }

            return true;
        }
    }
}
