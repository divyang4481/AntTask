using System;
using System.Collections.Generic;
using System.Text;

namespace AntTask
{
    /// <summary>
    ///  Exception thrown when a required property is missing.
    /// </summary>
    public class PropertyRequiredException : ApplicationException
    {
        private const string ERROR_MESSAGE = "The required property {0} for task {1} was null or empty.";

        /// <summary>
        ///   Create an instance of a property required exception when a property for a task
        ///   has not been set and it is required for the operation to complete.
        /// </summary>
        /// <param name="propertyName">The name of the MSBuild attribute that is missing</param>
        /// <param name="taskType">The name of the task requiring the property</param>
        public PropertyRequiredException(string propertyName, string taskType)
            : base(String.Format(ERROR_MESSAGE, propertyName, taskType))
        {
        }
    }
}
