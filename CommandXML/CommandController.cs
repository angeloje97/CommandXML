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
        public Action cleanUp;

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

        public CommandItem(Action action)
        {
            this.action += (element) => { action(); };
        }

        public CommandItem(Action<XmlElement> action)
        {
            this.action = action;
        }

        public void Invoke(XmlElement element)
        {
            if (!enabledCleanUp)
            {
                enabledCleanUp = true;
                CommandController.OnCleanUp += () => {
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
        public static bool runningCommand;


        List<string> commandLogs;
        readonly int maxLogs = 25;
        readonly string emptyLog = "|                                                                            |";
        string currentStatus = "idle";

        public static Action OnCleanUp;
        public static Action<CommandItem> OnRunCommand;


        public static void Initiate()
        {
            if (instance != null) return;
            instance = new CommandController();


            instance.ReadData();
            instance.CleanUp();
        }
        public static void Initiate(Dictionary<string, CommandItem> extraCommands)
        {
            foreach(KeyValuePair<string, CommandItem> pair in extraCommands)
            {
                if (commands.ContainsKey(pair.Key)) continue;
                commands.Add(pair.Key, pair.Value);
            }
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
            CreateStatus();
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

            void CreateStatus()
            {
                var statusElement = doc.CreateElement("Status");
                statusElement.SetAttribute("current", currentStatus);

                root.AppendChild(statusElement);
            }
        }
        void ReadData()
        {
            reading = true;

            while (reading)
            {

                Thread.Sleep(1000);
                var doc = CurrentDoc();

                var commandNodes = doc.GetElementsByTagName("Command");
                if (commandNodes.Count == 0) continue;

                bool hasInvoked = false;

                foreach(XmlElement commandNode in commandNodes)
                {
                    var commandSent = commandNode.GetAttribute("name");
                    if (commandSent.Equals("")) continue;
                    hasInvoked = true;
                    HandleCommand(commandNode, doc);

                }

                if (hasInvoked)
                {
                    UpdateConsole(doc);
                    ClearCommands(doc);
                    UpdateStatus("Idle", doc);
                    doc.Save(Path.Combine(path, fileName));
                }
                
            }

            reading = false;
        }
        public void HandleCommand(XmlElement element, XmlDocument document)
        {
            var commandString = element.GetAttribute("name");
            if (!commands.ContainsKey(commandString)) {
                WriteLine($"Unknown Command: {commandString}");
                return;
            };


            WriteLine($"Running Command: {commandString}", document);
            var command = commands[commandString];

            OnRunCommand?.Invoke(command);
            runningCommand = true;
            UpdateStatus($"Running Command {commandString}", document);
            document.Save(Path.Combine(path, fileName));

            try
            {
                command.Invoke(element);

            }catch(Exception e)
            {
                WriteLine($"Error when running command: {commandString}", document);
                Console.WriteLine($"Error when running command: {commandString}. \nStackTrace: {e.StackTrace}");
            }

            runningCommand = false;
            UpdateStatus($"Finish Running {commandString}",document);
            WriteLine($"Finished Running Command: {commandString}", document);
            document.Save(Path.Combine(path, fileName));

            Thread.Sleep(1000);
        }
        public void WriteLine(string str, XmlDocument? doc = null)
        {
            commandLogs.Add(str);
            bool save = doc == null;
            if (doc == null) doc = CurrentDoc();

            UpdateConsole(doc);

            if (save)
            {
                doc.Save(Path.Combine(path, fileName));
            }

        }
        public void ClearCommands(XmlDocument doc) 
        {
            XmlNodeList dataNodes = doc.GetElementsByTagName("Command");
            


            for (int i = 0; i < dataNodes.Count; i++)
            {
                var node = dataNodes[i];

                if (node != null)
                {
                    var parentNode = node.ParentNode;

                    if (parentNode != null && i != 0)
                    {
                        parentNode.RemoveChild(node);
                        continue;
                    }

                    if (node.Attributes != null)
                    {
                        node.Attributes.RemoveAll();
                    }

                    if(node is XmlElement element)
                    {
                        element.SetAttribute("name", "");
                    }
                }


            }
        }
        void UpdateConsole(XmlDocument doc)
        {
            var consoles = doc.GetElementsByTagName("Console");

            foreach(XmlElement console in consoles)
            {

                while (console.HasChildNodes)
                {
                    console.RemoveChild(console.FirstChild);
                }

                var exceedsMaxLogs = commandLogs.Count > maxLogs;
                var start = exceedsMaxLogs ? commandLogs.Count - maxLogs : 0;
                var leftOvers = maxLogs - (commandLogs.Count - start);

                while (leftOvers > 0)
                {
                    console.AppendChild(doc.CreateComment(emptyLog));

                    leftOvers--;
                }

                for (int i = start; i < commandLogs.Count; i++)
                {
                    var log = commandLogs[i];
                    console.AppendChild(doc.CreateComment(log));

                    leftOvers--;
                }
            }
        }
        
        void UpdateStatus(string current, XmlDocument doc)
        {
            var statuses = doc.GetElementsByTagName("Status");
            currentStatus = current;

            foreach(XmlElement element in statuses)
            {
                element.SetAttribute("current", currentStatus);
                return;
            }
        }
        XmlDocument CurrentDoc()
        {
            var doc = new XmlDocument();
            try
            {
                doc.Load(Path.Combine(path, fileName));
                return doc;
            }
            catch(Exception)
            {
                CreateNewXMLDoc();
                doc.Load(Path.Combine(path, fileName));
                return doc;
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

        static Dictionary<string, CommandItem> commands = new Dictionary<string, CommandItem> {
            { "SayHello", new CommandItem(
                    "Prints Hello World",
                    (element) => {
                        Console.WriteLine("Hello World");
                    }
                ) },
            { "LongTask", new CommandItem(() => {
                Console.WriteLine("Running Long Task");
                Thread.Sleep(10000);
            })},
            { "ThrowError", new CommandItem(() => {
                throw new Exception();
            }) },
            { "End", new CommandItem(
                    "Prints, ends the console",
                    (element) => {
                        reading = false;
                    }
                ) },
            
        };
    }
}
