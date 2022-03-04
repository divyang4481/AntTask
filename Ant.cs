using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Reflection;
using System.Text.RegularExpressions;
using System.IO;

namespace AntTask
{
    /// <summary>
    ///   MS Build tasks to wrap the calling of an ant build script.  This tasks replaces some of the logic in the Ant launcher script (ant.cmd)
    ///   And calls the Ant launcher directly from a java.exe command.
    ///   
    ///   original code: TeampriseBuildExtensions-1.2.0.397R http://labs.teamprise.com/build/extensions.html
    /// </summary>
    public class Ant : ToolTask
    {
        /// <summary>
        ///   The default build file used, set to "build.xml"
        /// </summary>
        public static readonly string DEFAULT_BUILD_FILE = "build.xml";

        #region Privates

        private string antHome;
        private bool autoproxy;

        private string buildFile = DEFAULT_BUILD_FILE;

        /// <summary>
        ///   Regular expressesion used to identify javac compile warning in default ant log.
        ///   <remarks>This has so far only been tested against the Sun Java compiler</remarks>
        /// </summary>
        protected Regex CompileWarningRegEx
        {
            get
            {
                if (compileWarningRegEx == null)
                {
                    switch (Language)
                    {
                        case "ja":
                            compileWarningRegEx = new Regex(@"^.+?\ \[javac]\ (?<src>.+):(?<line>\d+):\ (warning|警告):(?<msg>.+)$");
                            break;
                        case "en":
                        default:
                            compileWarningRegEx = new Regex(@"^.+?\ \[javac]\ (?<src>.+):(?<line>\d+):\ warning:(?<msg>.+)$");
                            break;
                    }
                }
                return compileWarningRegEx;
            }
        }
        private Regex compileWarningRegEx;

        private bool debug;
        private string inputhandler;
        private string javaHome;
        private bool keepGoing;
        private string lib;
        private string listener;
        private string logger;
        private string main;
        private bool noclasspath;
        private bool noinput;
        private bool nouserlib;
        private Dictionary<string, string> properties = new Dictionary<string, string>();
        private string propertyFile;
        private List<string> targets = new List<string>();
        private bool verbose;
        private string language = "en";

        #endregion Privates


        #region Ant Task properties

        /// <summary>
        ///   Name of the build file to use, by default this is "build.xml" in the current directory.
        /// </summary>
        public string BuildFile
        {
            get { return buildFile; }
            set { buildFile = value; }
        }

        /// <summary>
        ///   Comma seperated list of ant targets to execute.
        /// </summary>
        public string Targets
        {
            get { return string.Join(",", targets.ToArray()); }
            set
            {
                foreach (string t in value.Split(','))
                {
                    targets.Add(t);
                }
            }
        }

        /// <summary>
        ///   Single Ant Target to execute.
        /// </summary>
        public string Target
        {
            set { targets.Add(value); }
        }

        /// <summary>
        ///   Instruct Ant to print debugging information.
        /// </summary>
        public bool Debug
        {
            get { return debug; }
            set { debug = value; }
        }

        /// <summary>
        ///   Instruct Ant to be extra verbose
        /// </summary>
        public bool Verbose
        {
            get { return verbose; }
            set { verbose = value; }
        }

        /// <summary>
        ///   Language for regex used to in ant log
        /// </summary>
        public string Language
        {
            get { return language; }
            set { language = value; }
        }

        /// <summary>
        ///   Specifies a path for Ant to search for jars and classes.
        /// </summary>
        public string Lib
        {
            get { return lib; }
            set { lib = value; }
        }

        /// <summary>
        ///   Specifiy an Ant class to perform logging.
        /// </summary>
        public string Logger
        {
            get { return logger; }
            set { logger = value; }
        }

        /// <summary>
        ///   Do not allow interactive input in Ant script
        /// </summary>
        public bool Noinput
        {
            get { return noinput; }
            set { noinput = value; }
        }

        /// <summary>
        ///   Add an instance of an Ant class as a project listener
        /// </summary>
        public string Listener
        {
            get { return listener; }
            set { listener = value; }
        }

        /// <summary>
        ///   Instruct Ant to execute all targets that do not depend on failed target(s)
        /// </summary>
        public bool KeepGoing
        {
            get { return keepGoing; }
            set { keepGoing = value; }
        }

        /// <summary>
        ///   Instruct Ant to load all properties from file with -D properties taking precedence
        /// </summary>
        public string PropertyFile
        {
            get { return propertyFile; }
            set { propertyFile = value; }
        }

        /// <summary>
        ///   Specifies the Ant class which will handle input requests
        /// </summary>
        public string InputHandler
        {
            get { return inputhandler; }
            set { inputhandler = value; }
        }

        /// <summary>
        ///  Run ant without using the jar files from ${user.home}/.ant/lib
        /// </summary>
        public bool NoUserLib
        {
            get { return nouserlib; }
            set { nouserlib = value; }
        }

        /// <summary>
        ///   Run ant without using CLASSPATH
        /// </summary>
        public bool NoClasspath
        {
            get { return noclasspath; }
            set { noclasspath = value; }
        }

        /// <summary>
        ///   In Java 1.5+, use the OS proxies
        /// </summary>
        public bool AutoProxy
        {
            get { return autoproxy; }
            set { autoproxy = value; }
        }

        /// <summary>
        ///   Override Ant's normal entry point with specified Ant class.
        /// </summary>
        public string Main
        {
            get { return main; }
            set { main = value; }
        }

        /// <summary>
        /// Properties to pass to Ant in "name=value;name2=value2" syntax.
        /// </summary>
        public string Properties
        {
            set
            {
                foreach (string pair in value.Split(';'))
                {
                    string[] prop = pair.Split('=');
                    if (prop.Length == 2)
                    {
                        properties[BuildHelper.Quote(prop[0].Trim())] = BuildHelper.Quote(prop[1].Trim());
                    }
                    else
                    {
                        Log.LogWarning(
                            "RunUser {0} is not in the correct format (name=value). RunUser therefore ignored.", prop);
                    }
                }
            }
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (string p in properties.Keys)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(';');
                    }
                    sb.Append(p).Append('=').Append(properties[p]);
                }
                return sb.ToString();
            }
        }

        /// <summary>
        ///   Location of Ant on Build Server.
        /// </summary>
        public string AntHome
        {
            get
            {
                return antHome;
            }
            set
            {
                antHome = value;
            }
        }

        /// <summary>
        ///  Location of Java home directory on build server.
        /// </summary>
        public string JavaHome
        {
            get
            {
                return javaHome;
            }
            set
            {
                javaHome = value;
                Log.LogMessage("JavaHome set to \"{0}\"", javaHome);
            }
        }

        /// <summary>
        ///   Additional JVM options when calling Ant, for example -Xmx640m 
        /// </summary>
        public string AntOptions
        {
            get;
            set;
        }

        #endregion Ant Task properties

        #region ToolTask Implementation

        /// <summary>
        ///     Execute
        ///   The main entry point when MSBuild executes this task.
        /// </summary>
        /// <returns>True if the task succeeds, false otherwise.</returns>
        public override bool Execute()
        {
            bool result = base.Execute();
            return result;
        }

        protected override MessageImportance StandardOutputLoggingImportance
        {
            get { return MessageImportance.Normal; }
        }

        /// <summary>
        ///   Log standard error and standard out.  This is overridden to look for relevant tasks such as javac or junit that we
        ///   might want to collect some data from.
        /// </summary>
        /// <param name="singleLine">A single line of stderr or stdout.</param>
        /// <param name="messageImportance">The importance of the message. Controllable via the StandardErrorImportance and StandardOutImportance properties.</param>
        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            // Log the message first, that way if anything else goes wrong we stand a better chance of finding out why.
            base.LogEventsFromTextOutput(singleLine, messageImportance);

            if (singleLine.EndsWith(":"))
            {
                // We are in a new target
            }
            else if (singleLine.IndexOf(" [javac] ") > 0)
            {
                Match match = CompileWarningRegEx.Match(singleLine);
                if (match.Success)
                {
                    Log.LogWarning(match.Groups["msg"].Value);
                }
            }
        }

        /// <summary>
        /// The name of the tool to execute, in this case it is java.exe
        /// </summary>
        protected override String ToolName
        {
            get { return "java.exe"; }
        }

        /// <summary>
        ///   Determines the full path to Java, if this path has not been explicitly specified by the user.
        /// </summary>
        /// <returns>The full path to Java</returns>
        protected override String GenerateFullPathToTool()
        {
            if (javaHome == null)
            {
                throw new PropertyRequiredException("JavaHome", GetType().Name);
            }

            string javaExe = "";
            FileInfo fi = new FileInfo(Path.Combine(javaHome, "bin" + Path.DirectorySeparatorChar + "java.exe"));
            if (fi.Exists)
            {
                javaExe = fi.FullName;
            }
            else
            {
                throw new Exception(String.Format("Unable to locate java in JavaHome directory '{0}'.", javaHome));
            }

            return javaExe;
        }

        /// <summary>
        ///  Implementation of ToolTask.GenerateCommandLineCommands.  Build up the
        ///  commands to make java fire up Ant.
        /// </summary>
        protected override String GenerateCommandLineCommands()
        {
            StringBuilder commands = new StringBuilder();
            if (!String.IsNullOrEmpty(AntOptions))
            {
                commands.Append(AntOptions + " ");
            }
            commands.Append("-jar ");
            commands.Append(BuildHelper.Quote(LocateAntLauncher(AntHome)));
            commands.AppendFormat(" -buildfile {0}", BuildHelper.Quote(BuildFile));
            if (Debug)
            {
                commands.Append(" -d");
            }
            if (Verbose)
            {
                commands.Append(" -v");
            }
            if (!string.IsNullOrEmpty(Lib))
            {
                commands.AppendFormat(" -lib {0}", Lib);
            }
            if (!string.IsNullOrEmpty(Logger))
            {
                commands.AppendFormat(" -logger {0}", Logger);
            }
            if (!string.IsNullOrEmpty(Listener))
            {
                commands.AppendFormat(" -listener {0}", Listener);
            }
            if (Noinput)
            {
                commands.Append(" -noinput");
            }
            if (KeepGoing)
            {
                commands.Append(" -k");
            }
            if (!string.IsNullOrEmpty(PropertyFile))
            {
                commands.AppendFormat(" -propertyfile {0}", PropertyFile);
            }
            if (!string.IsNullOrEmpty(InputHandler))
            {
                commands.AppendFormat(" -inputhandler {0}", InputHandler);
            }
            if (NoUserLib)
            {
                commands.Append(" -nouserlib");
            }
            if (NoClasspath)
            {
                commands.Append(" -noclasspath");
            }
            if (AutoProxy)
            {
                commands.Append(" -autoproxy");
            }
            if (!string.IsNullOrEmpty(Main))
            {
                commands.AppendFormat(" -main {0}", Main);
            }

            foreach (string key in properties.Keys)
            {
                commands.AppendFormat(" -D{0}={1}", key, properties[key]);
            }
            foreach (string target in targets)
            {
                commands.AppendFormat(" {0}", BuildHelper.Quote(target));
            }

            return commands.ToString();
        }

        private string LocateAntLauncher(string antHome)
        {
            if (antHome == null)
            {
                throw new PropertyRequiredException("AntHome", GetType().Name);
            }

            FileInfo antLauncherJar =
                new FileInfo(Path.Combine(antHome, "lib" + Path.DirectorySeparatorChar + "ant-launcher.jar"));
            if (!antLauncherJar.Exists)
            {
                Log.LogWarning("'{0}' calculated from ANT_HOME '{1}' doesn't exist", antLauncherJar.FullName, antHome);
            }

            return antLauncherJar.FullName;
        }

        #endregion ToolTask Implementation

    }
}
