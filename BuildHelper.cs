using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using System.IO;

namespace AntTask
{
    class BuildHelper
    {
        private static readonly char[] WHITESPACE_CHARS = new char[] { ' ', '\t', '\"' };

        /// <summary>
        ///   Put a quote around the argument if it contains whitespace.
        /// </summary>
        /// <param name="argument">command line argument</param>
        /// <returns>quoted argument only if necessary</returns>
        public static string Quote(string argument)
        {
            if (argument == null)
            {
                return null;
            }
            if (argument.IndexOfAny(WHITESPACE_CHARS) >= 0)
            {
                // Check to see if argument already quoted.
                if (argument.StartsWith("\"") && argument.EndsWith("\""))
                {
                    return argument;
                }
                if (argument.StartsWith("\'") && argument.EndsWith("\'"))
                {
                    return argument;
                }

                if (argument.IndexOf('"') >= 0)
                {
                    // contains a ", wrap in '
                    return string.Format("'{0}'", argument);
                }
                else
                {
                    // Just quote with "
                    return string.Format("\"{0}\"", argument);
                }
            }
            return argument;
        }
    }
}
