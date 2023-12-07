using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace CommandXML
{

    public class CommandItem
    {
        public string commandName;
        public string description;
        Action<XmlElement> action;
        public Dictionary<string, Type> validAttributes;
        public Action cleanUp;

        bool enabledCleanUp;

        bool running;

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

        public void Invoke(XmlElement element, Action<Exception> OnError = null)
        {


            if (!enabledCleanUp)
            {
                enabledCleanUp = true;
                if(cleanUp != null)
                {
                    CommandController.instance.cleanUpCommandItems.Add(this);
                }
            }

            running = true;

            try
            {
                action?.Invoke(element);

            }
            catch (Exception e)
            {
                OnError?.Invoke(e);
                
            }

            running = false;

            WaitTilDone();
        }

        public void InvokeCleanUp(Action<Exception> OnError = null)
        {
            try
            {
                cleanUp?.Invoke();

            }
            catch(Exception e)
            {
                OnError?.Invoke(e);
            }
        }

        public void WaitTilDone()
        {
            Thread.Sleep(1000);
            while (running) Thread.Sleep(250);
        }
    }
    internal class CommandController
    {
        public static CommandController instance;

        public static string path = "C:\\WorkSpace\\Porgramming\\Sandbox";
        public static string fileName = "Commands.xml";

        public static bool reading;
        public static bool runningCommand;
        public static bool runInBackground;
        bool stop;

        List<string> commandLogs;
        readonly int maxLogs = 25;
        readonly string emptyLog = "|                                                                            |";
        string currentStatus = "idle";

        public List<CommandItem> cleanUpCommandItems { get; set; }
        public Action<CommandItem> OnRunCommand { get; set; }
        public Action<CommandItem, Exception> OnError { get; set; }


        public static void Initiate()
        {
            if (instance != null) return;
            instance = new CommandController();
            UpdateCommandNames();

            if (runInBackground)
            {
                Task.Run(() => {
                    instance.ReadData();
                    instance.CleanUp();
                });
            }
            else
            {
                instance.ReadData();
                instance.CleanUp();
            }
        }

        static void UpdateCommandNames()
        {
            foreach(KeyValuePair<string, CommandItem> pairs in commands)
            {
                pairs.Value.commandName = pairs.Key;
            }
        }
        public static void Initiate(Dictionary<string, CommandItem> extraCommands, bool cleanCommands = false)
        {
            commands = new Dictionary<string, CommandItem>();
            foreach(KeyValuePair<string, CommandItem> pair in extraCommands)
            {
                if (commands.ContainsKey(pair.Key)) continue;
                commands.Add(pair.Key, pair.Value);
            }

            Initiate();
        }
        public CommandController()
        {
            commandLogs = new List<string>();
            cleanUpCommandItems = new List<CommandItem>();
            CreateNewXMLDoc();
        }
        XmlDocument CurrentDoc()
        {
            var doc = new XmlDocument();
            try
            {
                doc.Load(Path.Combine(path, fileName));
                return doc;
            }
            catch (Exception)
            {
                CreateNewXMLDoc();
                doc.Load(Path.Combine(path, fileName));
                return doc;
            }
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
                    var stringBuilder = new StringBuilder();

                    stringBuilder.Append($"Command:<{command.Key}>");

                    if (command.Value.validAttributes != null)
                    {
                        var stringList = new List<string>();
                        stringBuilder.Append(" Required Attributes: (");
                        foreach(KeyValuePair<string, Type> pair in command.Value.validAttributes)
                        {
                            stringList.Add($"{pair.Key}: {pair.Value}");
                        }

                        stringBuilder.Append(string.Join(", ", stringList));
                        stringBuilder.Append(")");
                    }

                    var description = command.Value.description;
                    if (!string.IsNullOrEmpty(description))
                    {
                        stringBuilder.Append($": Summary: {command.Value.description}");

                    }

                    controls.AppendChild(doc.CreateComment(stringBuilder.ToString()));
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
                if (stop) break;
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
                    ReadCommand(commandNode, doc);

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
        public void ReadCommand(XmlElement element, XmlDocument document)
        {
            var commandString = element.GetAttribute("name");
            if (!commands.ContainsKey(commandString)) {
                WriteLine($"Unknown Command: {commandString}");
                return;
            };

            var command = commands[commandString];

            if(command.validAttributes != null)
            {
                if (!ValidateAttributes(element, command.validAttributes))
                {
                    return;
                }
            }

            var isTask = CheckRunTask(element);

            if (isTask)
            {
                Task.Run(() => {
                    ProcessCommand($"Starting Command TASK: {command}", $"Finish Running TASK {commandString}" , false);
                });
            }
            else
            {
                ProcessCommand($"Starting Command {command}", $"Finished Command {command}");
            }

            


            Thread.Sleep(1000);

            void ProcessCommand(string startString, string endString, bool updateStatus = true)
            {


                runningCommand = true;
                OnRunCommand?.Invoke(command);

                if (updateStatus)
                {
                    UpdateStatus($"Running Command {command}", document);
                }

                WriteLine(startString);

                command.Invoke(element, (Exception e) => {
                    WriteLine($"Error when running command: {this}", document);
                    Console.WriteLine($"Error when running command: {this}. \nStackTrace: {e.StackTrace}");
                    Console.WriteLine($"Message: {e.Message}");
                    OnError?.Invoke(command, e);
                });


                runningCommand = false;
                if (updateStatus)
                {
                    UpdateStatus($"Finish Running {commandString}", document);
                }

                WriteLine(endString);
            }

        }
        public void WriteLine(string str, XmlDocument doc = null)
        {
            commandLogs.Add(str);
            bool save = doc == null;
            if (doc == null) doc = CurrentDoc();
            Console.WriteLine(str);
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

        public void WaitUntilReadingFinished()
        {
            while (reading)
            {
                Thread.Sleep(1000);
            }
        }

        public void StopReading()
        {
            stop = true;

            WaitUntilReadingFinished();
        }

        bool CheckRunTask(XmlElement element)
        {
            try
            {
                return bool.Parse(element.GetAttribute("task"));
            }catch
            {
                return false;
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
        public void CleanUp()
        {
            var doc = CurrentDoc();

            UpdateStatus("Running cleanup functions", doc);
            doc.Save(Path.Combine(path, fileName));

            foreach(var commandItem in cleanUpCommandItems)
            {
                WriteLine($"Running cleanup function from command: {commandItem}");

                commandItem.InvokeCleanUp((Exception e) => {
                    WriteLine($"Could not execute cleanup function for command: {commandItem} check console");
                    Console.WriteLine($"Error for cleanup function for command: {commandItem}\n{e.StackTrace}");
                    Console.Write($"Message: {e.Message}");
                });
                WriteLine($"Finished running cleanup function from command: {commandItem}");
            }

            //if (!File.Exists(Path.Combine(path, fileName))) return;
            //File.Delete(Path.Combine(path, fileName));
        }
        bool ValidateAttributes(XmlElement elementWithAttributes, Dictionary<string, Type> pairs)
        {
            bool valid = true;
            var invalidReason = new StringBuilder();
            foreach (KeyValuePair<string, Type> pair in pairs)
            {
                try
                {
                    var converter = TypeDescriptor.GetConverter(pair.Value);
                    if (!elementWithAttributes.HasAttribute(pair.Key)) {
                        throw new Exception($"Element does not have attribute: {pair.Key}");
                    };
                    var attributeValue = elementWithAttributes.GetAttribute(pair.Key);
                    var result = converter.ConvertFromString(attributeValue);

                }
                catch(Exception e)
                {
                    valid = false;
                    invalidReason.Append($"Can't convert attribute <{pair.Key}> to <{pair.Value}>\n");
                }
            }

            if (!valid)
            {
                instance.WriteLine($"Could not validate value types: \n{invalidReason}");
            }

            return valid;
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

            //Test Validate
            { "TestValidate", new CommandItem(
                (element)=>{

                    var alias = element.GetAttribute("alias");
                    var age = int.Parse(element.GetAttribute("age"));

                    Console.WriteLine($"{alias}({age})");
                
            }) {
                validAttributes = new Dictionary<string, Type>()
                {
                    { "alias", typeof(string) },
                    { "age", typeof(int) }
                }
            } },

            //End Reading Command
            { "End", new CommandItem(
                    (element) => {
                        reading = false;
                    }
                )},
            
        };
    }
}
