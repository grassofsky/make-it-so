using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MakeItSoLib;

namespace MakeItSo
{
    /// <summary>
    /// Create a configuration file for csharp project in the solution
    /// </summary><remarks>
    /// Project configuration files have the name [project-name].bff. Type
    /// will mostly be invoked from the 'master' fbuild.bff at the solution root.
    /// 
    /// The configuration files have:
    /// 
    /// {
    ///     CSAssembly('[project-name]-x64-Debug')
    ///     {
    ///         Using( .csx64BaseConfig )
    ///         .CompilerOptions = ...
    ///         .CompilerOutput = ...
    ///         .CompilerInputFiles = ...
    ///         .ComiplerReferences = ...
    ///         .PreBuildDependencies = ...
    ///     }
    ///     
    ///     (And similarly for x86 and Release configuration.)
    ///     
    ///     Alias( '[project-name]' ) { .Targets = { '[project-name]-x64-Debug', '[project-name]-x86-Debug', ... } }
    /// }
    /// 
    /// </remarks>
    class FastbuildFileBuilder_Project_CSharp
    {
        #region Public methods and properties

        /// <summary>
        /// Create configuration file for the project passed in.
        /// </summary>
        public static void createConfigurationFile(ProjectInfo_CSharp project)
        {
            new FastbuildFileBuilder_Project_CSharp(project);
        }

        #endregion

        #region Private functions

        /// <summary>
        /// Constructor
        /// </summary>
        private FastbuildFileBuilder_Project_CSharp(ProjectInfo_CSharp project)
        {
            if (project.ProjectType == ProjectInfo.ProjectTypeEnum.INVALID)
            {
                return;
            }

            m_projectInfo = project;
            try
            {
                // We create the file '[project-name].bff'
                string path = String.Format("{0}/{1}.bff", m_projectInfo.RootFolderAbsolute, m_projectInfo.Name);
                m_file = new StreamWriter(path, false);
                m_file.NewLine = "\n";
                m_file.WriteLine("{");

                // We create variables...
                createReferencesVariables();
                createFilesVariable();
                createFlagsVariables();

                createConfigurationTargets();

                m_file.WriteLine("}");
            }
            finally
            {
                if (m_file != null)
                {
                    m_file.Close();
                    m_file.Dispose();
                }
            }
        }


        /// <summary>
        /// Creates a REFERENCES variable for each configuration in the project.
        /// </summary>
        private void createReferencesVariables()
        {
            // We create one set of references for each configuration...
            foreach (ProjectConfigurationInfo_CSharp configurationInfo in m_projectInfo.getConfigurationInfos())
            {
                string variableName = getReferencesVariableName(configurationInfo);

                // The variable holds a comma-separated list of references...
                string value = "";
                List<ReferenceInfo> referenceInfos = configurationInfo.getReferenceInfos();
                foreach (ReferenceInfo referenceInfo in referenceInfos)
                {
                    value += ("'" + referenceInfo.AbsolutePath + "',");
                }
                value = value.TrimEnd(',');

                m_file.WriteLine("." + variableName + " = {" + value + "}");
            }
            m_file.WriteLine("");
        }

        /// <summary>
        /// Creates files variable for compile
        /// </summary>
        private void createFilesVariable()
        {
            List<string> files = new List<string>();
            foreach (string file in m_projectInfo.getFiles())
            {
                files.Add("'" + m_projectInfo.RootFolderAbsolute + file + "'");
            }
            m_file.WriteLine("  .InputFiles = {" + Utils.join(",", files) + "}");
        }

        /// <summary>
        /// Creates variables for the compiler flags for each
        /// configuration in the project.
        /// </summary>
        private void createFlagsVariables()
        {
            foreach (ProjectConfigurationInfo_CSharp configuration in m_projectInfo.getConfigurationInfos())
            {
                createConfigurationFlagsVariable(configuration);
            }
        }

        /// <summary>
        /// Creates compiler flags for the configuration passed in.
        /// </summary>
        private void createConfigurationFlagsVariable(ProjectConfigurationInfo_CSharp configurationInfo)
        {
            string variableName = getFlagsVariableName(configurationInfo);
            string flags = "";

            // Target
            if (m_projectInfo.ProjectType == ProjectInfo.ProjectTypeEnum.CSHARP_EXECUTABLE)
            {
                flags += " /target:exe";
            }
            else if (m_projectInfo.ProjectType == ProjectInfo.ProjectTypeEnum.CSHARP_LIBRARY)
            {
                flags += " /target:library";
            }
            else if (m_projectInfo.ProjectType == ProjectInfo.ProjectTypeEnum.CSHARP_WINFORMS_EXECUTABLE)
            {
                flags += " /target:winexe";
            }

            // Platform
            flags += " /platform:" + configurationInfo.Platform;

            // Optimize...
            if (configurationInfo.Optimize == true)
            {
                flags += " /optimize+ ";
            }
            else
            {
                flags += " /optimize- ";
            }

            // Treat warnings as errors...
            if (configurationInfo.ThreatWarningsAsErrors == true)
            {
                flags += " /warnaserror+ ";
            }

            // Defined constants...
            foreach (string definedConstant in configurationInfo.getDefinedConstants())
            {
                flags += (" /define:" + definedConstant + " ");
            }

            // Debug build...
            if (configurationInfo.Debug == true)
            {
                flags += " /debug+ ";
            }

            // Type of debug info...
            if (configurationInfo.DebugInfo != "")
            {
                flags += (" /debug:" + configurationInfo.DebugInfo + " ");
            }

            // Warnings to ignore...
            List<string> warningsToIgnore = configurationInfo.getWarningsToIgnore();
            if (warningsToIgnore.Count > 0)
            {
                flags += " /nowarn:";
                foreach (string warningToIgnore in warningsToIgnore)
                {
                    flags += (warningToIgnore + ",");
                }
                flags = flags.TrimEnd(',') + " ";
            }

            // File alignment...
            flags += (" /filealign:" + configurationInfo.FileAlignment + " ");

            // Warning level...
            flags += (" /warn:" + configurationInfo.WarningLevel + " ");

            m_file.WriteLine("." + variableName + " = '" + flags + "'");
            m_file.WriteLine("");
        }

        /// <summary>
        /// Creates a target for each configuration.
        /// </summary>
        private void createConfigurationTargets()
        {
            ///     Alias( '[project-name]' ) { .Targets = { '[project-name]-x64-Debug', '[project-name]-x86-Debug', ... } }
            List<string> targetList = new List<string>();
            foreach (ProjectConfigurationInfo_CSharp configurationInfo in m_projectInfo.getConfigurationInfos())
            {
                // Create configuration target
                createConfigurationTarget(configurationInfo);
                targetList.Add("'" + configurationInfo.TargetName + "'");
            }
            string targets = "Alias( '" + m_projectInfo.Name + "' ) { .Targets = { " + Utils.join(", ", targetList) + " }}";
            m_file.WriteLine(targets);
        }

        /// <summary>
        /// Creates configuration target
        /// </summary>
        private void createConfigurationTarget(ProjectConfigurationInfo_CSharp configurationInfo)
        {
            m_file.WriteLine("CSAssembly( '" + configurationInfo.TargetName + "' )");
            m_file.WriteLine("{");
            m_file.WriteLine("  Using( .cs" + configurationInfo.Platform + "BaseConfig )");
            string postfix = ".exe";
            if (m_projectInfo.ProjectType == ProjectInfo.ProjectTypeEnum.CSHARP_LIBRARY)
            {
                postfix = ".dll";
            }
            m_file.WriteLine("  .CompilerOutput     = '" + configurationInfo.OutputFolderAbsolute + m_projectInfo.Name + postfix + "'");
            m_file.WriteLine("  .CompilerOptions    + ." + getFlagsVariableName(configurationInfo));
            m_file.WriteLine("                      + '\"%1\"'");
            m_file.WriteLine("  .CompilerInputFiles = .InputFiles");
            m_file.WriteLine("  .CompilerReferences = ." + getReferencesVariableName(configurationInfo));
            List<string> requiredTargets = new List<string>();
            foreach (ProjectInfo requiredProject in m_projectInfo.getRequiredProjects())
            {
                requiredTargets.Add("'" + requiredProject.Name + "'");
            }
            if (requiredTargets.Count != 0)
            {
                m_file.WriteLine("  .PreBuildDependencies = { " + Utils.join(", ", requiredTargets) + " }");
            }
            m_file.WriteLine("}");
            m_file.WriteLine("");
        }

        /// <summary>
        /// Returns the reference variable name for the configuration passed in.
        /// For example "Debug_x64_References".
        /// </summary>
        private string getReferencesVariableName(ProjectConfigurationInfo_CSharp configuration)
        {
            return configuration.Name + "_" + configuration.Platform + "_References";
        }

        /// <summary>
        /// Return the flags variable name for the configuration passed in.
        /// For example "Debug_x64_Compile_Flags".
        /// </summary>
        private string getFlagsVariableName(ProjectConfigurationInfo_CSharp configuration)
        {
            return configuration.Name + "_" + configuration.Platform + "_Compile_Flags";
        }

        #endregion

        #region Private data

        // The passed project data that we are creating the configuration from...
        private ProjectInfo_CSharp m_projectInfo = null;

        // The file we write to...
        private StreamWriter m_file = null;

        #endregion
    }
}
