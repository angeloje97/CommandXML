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
        Action<XmlElement> action;
        public Action? cleanUp;

        bool enabledCleanUp;

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

        public void Invoke(XmlElement element)
        {
            if (!enabledCleanUp)
            {
                enabledCleanUp = true;
                CommandController.instance.OnCleanUp += () => {
                    cleanUp?.Invoke();
                };
            }

            action?.Invoke(element);
        }
    }
    internal class CommandController
    {
        public static CommandController instance;

        static readonly string path = "C:\\WorkSpace\\Porgramming\\Sandbox";
        static readonly string fileName = "Commands.xml";

        public static bool reading;


        List<string> commandLogs;
        readonly int maxLogs = 25;
        readonly string emptyLog = "|                                                                            |";

        public Action? OnCleanUp;


        public static void Initiate()
        {
            if (instance != null) return;
            instance = new CommandController();


            instance.ReadData();
            instance.CleanUp();
        }

        public CommandController()
        {
            commandLogs = new List<string>();
            CreateNewXMLDoc();
        }

        void CreateNewXMLDoc()
        {
            var doc = new XmlDocument();
            var root = doc.CreateElement("CommandXML");


            doc.AppendChild(root);


            CreateConsole();
            CreateControls();

            doc.Save(Path.Combine(path, fileName));

            void CreateConsole()
            {
                var console = doc.CreateElement("Console");

                root.AppendChild(console);
                UpdateConsole(doc);
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

        void ReadData()
        {
            reading = true;

            while (reading)
            {

                Thread.Sleep(1000);
                var doc = new XmlDocument();


                try
                {
                    doc.Load(Path.Combine(path, fileName));

                }
                catch (Exception)
                {
                    CreateNewXMLDoc();
                    doc.Load(Path.Combine(path, fileName));
                }

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
                    UpdateConsole(doc);
                    ClearCommands(doc);
                    doc.Save(Path.Combine(path, fileName));
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
            commands[commandString].Invoke(element);
        }

        public void WriteLine(string str)
        {
            commandLogs.Add(str);
        }

        public void ClearCommands(XmlDocument doc) 
        {
            var dataNodes = doc.GetElementsByTagName("Command");
            
            foreach(XmlElement element in dataNodes)
            {
                element.SetAttribute("name", "");
            }
        }

        void UpdateConsole(XmlDocument doc)
        {
            var consoles = doc.GetElementsByTagName("Console");

            foreach(XmlElement console in consoles)
            {
                console.RemoveAll();

                int leftOvers = maxLogs;
                var end = commandLogs.Count < maxLogs ? 0 : commandLogs.Count - maxLogs;

                for (int i = commandLogs.Count - 1; i >= end; i--)
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

        public void CleanUp()
        {
            OnCleanUp?.Invoke();
            if (!File.Exists(Path.Combine(path, fileName))) return;
            File.Delete(Path.Combine(path, fileName));
        }
        public bool Validate(XmlElement elementWithAttributes, Dictionary<string, Type> pairs)
        {
            foreach (XmlAttribute attribute in elementWithAttributes)
            {
                if (pairs.ContainsKey(attribute.Name))
                {

                }
            }

            return true;
        }

        public static readonly Dictionary<string, CommandItem> commands = new() {
            { "SayHello", new CommandItem(
                    "Prints Hello World",
                    (element) => {
                        Console.WriteLine("Hello World");
                    }
                ) },
            { "End", new CommandItem(
                    "Prints, ends the console",
                    (element) => {
                        reading = false;
                    }
                ) }
        };

        
    }
}
