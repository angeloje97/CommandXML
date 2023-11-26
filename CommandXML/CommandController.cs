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
        public string commandName;
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

        public override string ToString()
        {
            return commandName;
        }

        public void Invoke(XmlElement element)
        {
            if (!enabledCleanUp)
            {
                enabledCleanUp = true;
                if(cleanUp != null)
                {
                    CommandController.instance.cleanUpCommandItems.Add(this);
                }
            }

            action?.Invoke(element);
        }

        public void InvokeCleanUp()
        {
            cleanUp?.Invoke();
        }
    }
    internal class CommandController
    {
        public static CommandController instance;

        public static string path = "C:\\WorkSpace\\Porgramming\\Sandbox";
        public static string fileName = "Commands.xml";

        public static bool reading;
        public static bool runningCommand;


        List<string> commandLogs;
        readonly int maxLogs = 25;
        readonly string emptyLog = "|                                                                            |";
        string currentStatus = "idle";

        public List<CommandItem> cleanUpCommandItems;
        public Action<CommandItem> OnRunCommand;
        

        public static void Initiate()
        {
            if (instance != null) return;
            instance = new CommandController();
            UpdateCommandNames();

            instance.ReadData();
            instance.CleanUp();
        }

        static void UpdateCommandNames()
        {
            foreach(KeyValuePair<string, CommandItem> pairs in commands)
            {
                pairs.Value.commandName = pairs.Key;
            }
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
            cleanUpCommandItems = new();
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


            var command = commands[commandString];
            WriteLine($"Running Command: {command}", document);

            OnRunCommand?.Invoke(command);
            runningCommand = true;
            UpdateStatus($"Running Command {command}", document);
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
            var doc = CurrentDoc();

            UpdateStatus("Running cleanup functions", doc);
            doc.Save(Path.Combine(path, fileName));

            foreach(var commandItem in cleanUpCommandItems)
            {
                WriteLine($"Running cleanup function from command: {commandItem}");
                try
                {
                    commandItem.InvokeCleanUp();
                }catch(Exception e)
                {
                    WriteLine($"Could not execute cleanup function for command: {commandItem} check console");
                    Console.WriteLine($"Error for cleanup function for command: {commandItem}\n{e.StackTrace}");
                }
                WriteLine($"Finished running cleanup function from command: {commandItem}");
            }

            //if (!File.Exists(Path.Combine(path, fileName))) return;
            //File.Delete(Path.Combine(path, fileName));
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
            //Say Hello Command
            { "SayHello", new CommandItem(
                    "Prints Hello World",
                    (element) => {
                        Console.WriteLine("Hello World");
                    }
                ) },

            //Long Task Command
            { "LongTask", new CommandItem(() => {
                Console.WriteLine("Running Long Task");
                Thread.Sleep(10000);
            }) { cleanUp = () => {
                Console.WriteLine("Running cleanup");
                Thread.Sleep(5000);
            } } },

            //Throw Error Command
            { "ThrowError", new CommandItem(() => {
                throw new Exception();
            })
            {cleanUp = () => {
                throw new Exception();
            } }
            },

            //End Reading Command
            { "End", new CommandItem(
                    "Prints, ends the console",
                    (element) => {
                        reading = false;
                    }
                )},
            
        };
    }
}
