using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MakeItSoLib;

namespace MakeItSo
{
    /// <summary>
    /// Create a configure file for one c++ project in the solution
    /// </summary><remarks>
    /// Project configure files have the name [project-name].bff. Type will
    /// mostly be invoked from the 'master' fbuild.bff at the solution root.
    /// 
    /// - If the target is exe, the configure files have:
    /// 
    /// {
    ///     ObjectList('[project-name]-x64-Debug-Objs')
    ///     {
    ///         Using( .x64BaseConfig )
    ///         .CompilerInputPath = ...
    ///         .CompilerOutputPath = ...
    ///         .CompilerOptions + ...
    ///     }
    /// 
    ///     Executable('[project-name]-x64-Debug')
    ///     {
    ///         Using( .x64BaseConfig )
    ///     }
    /// 
    ///     (And similarly for the x86 and Release configuration.)
    ///     
    ///     Alias( '[project-name]' ) { .Targets = { '[project-name]-x64-Debug', '[project-name]-x86-Debug', ... } }
    /// }
    /// 
    /// - If the target is dll, the configure files have:
    /// 
    /// {
    ///     ObjectList('[project-name]-x64-Debug-Objs')
    ///     {
    ///         Using( .x64BaseConfig )
    ///         .CompilerInputPath = ...
    ///         .CompilerOutputPath = ...
    ///         .CompilerOptions + ...
    ///     }
    /// 
    ///     Dll('[project-name]-x64-Debug')
    ///     {
    ///         Using( .x64BaseConfig )
    ///         .Libraries = {'[project-name]-x64-Debug-Objs' }
    ///         .LinkerOptions + ...
    ///         .LinkerOutput = ...
    ///         .PreBuildDependencies = ...
    ///     }
    ///     
    ///     (And similarly for the x86 and Release configuration.)
    ///
    ///     Alias( '[project-name]' ) { .Targets = { '[project-name]-x64-Debug', '[project-name]-x86-Debug', ...} }
    /// }
    /// 
    /// - If the target is static library, the configure files have:
    /// 
    /// {
    ///     Library('[project-name]-x64-Debug')
    ///     {
    ///         Using( .x64BaseConfig )
    ///         .CompilerInputPath = ...
    ///         .CompilerInputPattern = ...
    ///         .CompilerOutputPath = ...
    ///         .LibrarianOutput = ...
    ///         .LinkerOptions + ...
    ///     }
    ///     
    ///     (And similarly for the x86 and Release configuration.)
    ///     Alias('[project-name]') { .Targets = { '[project-name]-x64-Debug', ... } }
    /// }
    /// 
    /// </remarks>
    class FastbuildFileBuilder_Project_CPP
    {
        #region Public methods and properties

        /// <summary>
        /// Create configure file for the project passed in.
        /// </summary>
        public static void createConfigurationFile(ProjectInfo_CPP project)
        {
            new FastbuildFileBuilder_Project_CPP(project);
        }

        #endregion

        #region Private functions

        /// <summary>
        /// Constructor
        /// </summary>
        private FastbuildFileBuilder_Project_CPP(ProjectInfo_CPP project)
        {
            if (project.ProjectType == ProjectInfo.ProjectTypeEnum.INVALID)
            {
                return;
            }

            m_projectInfo = project;
            m_projectConfig = MakeItSoConfig.Instance.getProjectConfig(m_projectInfo.Name);

            try
            {
                // We create the file '[project-name].bff'
                string path = String.Format("{0}/{1}.bff", m_projectInfo.RootFolderAbsolute, m_projectInfo.Name);
                m_file = new StreamWriter(path, false);
                m_file.NewLine = "\n";
                m_file.WriteLine("{");

                // We Create variables
                createIncludePathVariables();
                createLibraryPathVariables();
                createLibrariesVariables();
                createPreprocessorDefinitionsVariables();
                //createImplicitlyLinkedObjectsVariables();
                createCompilerFlagsVariables();
                createLinkerFlagsVariables();

                // We target for each configuration and all...
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
        /// We Create include path variables for the various configurations.
        /// </summary>
        private void createIncludePathVariables()
        {
            // We create an include path for each configuration...
            m_file.WriteLine("// Include paths...");
            foreach (ProjectConfigurationInfo_CPP configuration in m_projectInfo.getConfigurationInfos())
            {
                // The variable name...
                string variableName = getIncludePathVariableName(configuration);

                // Prepare include paths
                // All paths should be absolute path
                var includePaths = configuration.getIncludePaths().Select(
                    path => String.Format("/I{0}", Utils.quote(generateFullPath(path)))
                    ).ToList();

                // We write the variable...
                m_file.WriteLine(".{0} = ' {1}'", variableName, Utils.join(" ", includePaths));
            }

            m_file.WriteLine("");
        }

        /// <summary>
        /// We create library path variables for the various configurations.
        /// </summary>
        private void createLibraryPathVariables()
        {
            // We create a library path for each configuration...
            m_file.WriteLine("// Library paths...");
            foreach (ProjectConfigurationInfo_CPP configuration in m_projectInfo.getConfigurationInfos())
            {
                // The variable name...
                string variableName = getLibraryPathVariableName(configuration);

                // The library path...
                var libraryPaths = configuration.getLibraryPaths().Select(
                        path => String.Format("/LIBPATH:{0}", Utils.quote(generateFullPath(path)))
                    );

                // We write the variable...
                m_file.WriteLine(".{0} = ' {1}'", variableName, Utils.join(" ", libraryPaths));
            }

            m_file.WriteLine("");
        }

        /// <summary>
        /// Creates variables that hold the list of additional libraries
        /// for each configuration.
        /// </summary>
        private void createLibrariesVariables()
        {
            // We create a library path for each configuration...
            m_file.WriteLine("// Additional libraries...");
            foreach (ProjectConfigurationInfo_CPP configuration in m_projectInfo.getConfigurationInfos())
            {
                // The variable name...
                string variableName = getLibrariesVariableName(configuration);

                // The libraries...
                var libraries = configuration.getLibraryRawNames().Select(
                        libraryName => string.Format("{0}.lib", libraryName)
                    ).ToList();
                var invalid = libraries.Find(libraryName => libraryName.IndexOf('$') != -1);
                if (!string.IsNullOrEmpty(invalid))
                {
                    libraries.Remove(invalid);
                }
                // If we have some libraries, we surround them with start-group
                // and end-group tags. This is needed as otherwise gcc is sensitive
                // to the order than libraries are declared...
                string librariesStr = (libraries.Count > 0) ?
                    String.Format("{0}", Utils.join(" ", libraries)) :
                    string.Empty;

                // We write the variable...
                m_file.WriteLine(".{0} = ' {1}'", variableName, librariesStr);
            }

            m_file.WriteLine("");
        }

        /// <summary>
        /// Creates variables to hold the preprocessor definitions for each
        /// configuration we're building.
        /// </summary>
        private void createPreprocessorDefinitionsVariables()
        {
            // We create an collection of preprocessor-definitions
            // for each configuration...
            m_file.WriteLine("// Preprocessor definitions...");
            foreach (ProjectConfigurationInfo_CPP configuration in m_projectInfo.getConfigurationInfos())
            {
                // The variable name...
                string variableName = getPreprocessorDefinitionsVariableName(configuration);

                // The definitions...
                var definitions = configuration.getPreprocessorDefinitions().Select(
                        d => string.Format("/D{0}", d)
                    ).ToList();

                // Add Unicode defines
                if (configuration.CharacterSet == CharacterSet.Unicode)
                {
                    definitions.Add("/DUNICODE");
                }

                // We write the variable...
                m_file.WriteLine(".{0} = ' {1}'", variableName, Utils.join(" ", definitions));
            }

            m_file.WriteLine("");
        }

        /// <summary>
        /// Creates variables for the compiler flags for each configuration.
        /// </summary>
        private void createCompilerFlagsVariables()
        {
            // We create an collection of compiler flags for each configuration...
            m_file.WriteLine("// Compiler flags...");
            foreach (ProjectConfigurationInfo_CPP configuration in m_projectInfo.getConfigurationInfos())
            {
                // The variable name...
                string variableName = getCompilerFlagsVariableName(configuration);

                // The flags...
                var flags = new List<string>();
                flags.AddRange(configuration.getCompilerFlags());

                // We write the variable...
                m_file.WriteLine(".{0} = ' {1}'", variableName, Utils.join(" ", flags));
            }

            m_file.WriteLine("");
        }

        /// <summary>
        /// Creates variables for the linker flags for each configuration.
        /// </summary>
        private void createLinkerFlagsVariables()
        {
            // We create an collection of linker flags for each configuration...
            m_file.WriteLine("// Linker flags...");
            foreach (ProjectConfigurationInfo_CPP configuration in m_projectInfo.getConfigurationInfos())
            {
                // The variable name...
                string variableName = getLinkerFlagsVariableName(configuration);

                // The flags...
                var flags = new List<string>();
                flags.AddRange(configuration.getLinkerFlags());

                // We write the variable...
                m_file.WriteLine(".{0} = ' {1}'", variableName, Utils.join(" ", flags));
            }

            m_file.WriteLine("");
        }

        /// <summary>
        /// Creates a target for each configuration.
        /// </summary>
        private void createConfigurationTargets()
        {
            ///     Alias( '[project-name]' ) { .Targets = { '[project-name]-x64-Debug', '[project-name]-x86-Debug', ... } }
            List<string> targetList = new List<string>();
            foreach (ProjectConfigurationInfo_CPP configurationInfo in m_projectInfo.getConfigurationInfos())
            {
                // Create configuration target
                createConfigurationTarget(configurationInfo);
                targetList.Add("'" + configurationInfo.TargetName + "'");
            }
            string targets = "Alias( '" + m_projectInfo.Name + "' ) { .Targets = { " + Utils.join(", ", targetList) + " }}";
            m_file.WriteLine(targets);
        }

        /// <summary>
        /// Create configuration target
        /// </summary>
        private void createConfigurationTarget(ProjectConfigurationInfo_CPP configurationInfo)
        {
            if (m_projectInfo.ProjectType == ProjectInfo.ProjectTypeEnum.CPP_EXECUTABLE ||
                m_projectInfo.ProjectType == ProjectInfo.ProjectTypeEnum.CPP_DLL)
            {
                // Set object list
                m_file.WriteLine("ObjectList('" + configurationInfo.TargetName + "-objs')");
                m_file.WriteLine("{");
                m_file.WriteLine("  Using( ." + configurationInfo.Platform + "BaseConfig )");
                m_file.WriteLine("  .CompilerInputPath  = '" + m_projectInfo.RootFolderAbsolute + "'");
                m_file.WriteLine("  .CompilerOutputPath = '" + configurationInfo.IntermediateFolderAbsolute + "'");
                m_file.WriteLine("  .CompilerOptions    + ." + getPreprocessorDefinitionsVariableName(configurationInfo));
                m_file.WriteLine("                      + ." + getIncludePathVariableName(configurationInfo));
                m_file.WriteLine("                      + ." + getCompilerFlagsVariableName(configurationInfo));
                m_file.WriteLine("}");
                m_file.WriteLine("");

                // Set Executable or DLL by ProjectType
                if (m_projectInfo.ProjectType == ProjectInfo.ProjectTypeEnum.CPP_EXECUTABLE)
                {
                    m_file.WriteLine("Executable('" + configurationInfo.TargetName + "')");
                }
                else
                {
                    m_file.WriteLine("DLL('" + configurationInfo.TargetName + "')");
                }
                m_file.WriteLine("{");
                m_file.WriteLine("  Using( ." + configurationInfo.Platform + "BaseConfig )");
                m_file.WriteLine("  .Libraries            = {'" + configurationInfo.TargetName + "-objs'}");
                m_file.WriteLine("  .LinkerOptions        + ." + getLibraryPathVariableName(configurationInfo));
                m_file.WriteLine("                        + ." + getLinkerFlagsVariableName(configurationInfo));
                m_file.WriteLine("                        + ." + getLibrariesVariableName(configurationInfo));
                if (m_projectInfo.ProjectType == ProjectInfo.ProjectTypeEnum.CPP_DLL)
                {
                    m_file.WriteLine("                        + ' /DLL'");
                }
                m_file.WriteLine("                        + ' /IMPLIB:\"" + configurationInfo.DynamicLibOutputPath + "\"'");
                if (m_projectInfo.ProjectType == ProjectInfo.ProjectTypeEnum.CPP_DLL)
                {
                    m_file.WriteLine("  .LinkerOutput         = '" + configurationInfo.OutputFolderAbsolute + m_projectInfo.Name + ".dll'");
                }
                else
                {
                    m_file.WriteLine("  .LinkerOutput         = '" + configurationInfo.OutputFolderAbsolute + m_projectInfo.Name + ".exe'");
                }
                List<string> requiredTargets = new List<string>();
                foreach (ProjectInfo requiredProject in m_projectInfo.getRequiredProjects()) 
                {
                    requiredTargets.Add( "'" + requiredProject.Name + "'");
                }
                if (requiredTargets.Count != 0)
                {
                    m_file.WriteLine("  .PreBuildDependencies = { " + Utils.join(", ", requiredTargets) + " }");
                }
                m_file.WriteLine("}");
                m_file.WriteLine("");

            }
            else if (m_projectInfo.ProjectType == ProjectInfo.ProjectTypeEnum.CPP_STATIC_LIBRARY)
            {
                m_file.WriteLine("Library('" + configurationInfo.TargetName + "')");
                m_file.WriteLine("{");
                m_file.WriteLine("  Using( ." + configurationInfo.Platform + "BaseConfig )");
                m_file.WriteLine("  .CompilerInputPath     = '" + m_projectInfo.RootFolderAbsolute + "'");
                m_file.WriteLine("  .CompilerInputPattern  = { '*.cc', '*.cpp', '*.c' }");
                m_file.WriteLine("  .CompilerOutputPath    = '" + configurationInfo.IntermediateFolderAbsolute + "'");
                m_file.WriteLine("  .CompilerOptions       + ." + getPreprocessorDefinitionsVariableName(configurationInfo));
                m_file.WriteLine("                         + ." + getIncludePathVariableName(configurationInfo));
                m_file.WriteLine("                         + ." + getCompilerFlagsVariableName(configurationInfo));
                m_file.WriteLine("  .LibrarianOutput       = '" + configurationInfo.DynamicLibOutputPath + "'");
                m_file.WriteLine("  .LinkerOptions        + ." + getLibraryPathVariableName(configurationInfo));
                m_file.WriteLine("                        + ." + getLinkerFlagsVariableName(configurationInfo));
                m_file.WriteLine("                        + ." + getLibrariesVariableName(configurationInfo));
                m_file.WriteLine("}");
            }
        }

        /// <summary>
        /// Returns the include-path variable name for the configuration passed in.
        /// For example "Debug_Include_Path".
        /// </summary>
        private string getIncludePathVariableName(ProjectConfigurationInfo_CPP configuration)
        {
            return configuration.Name + "_" + configuration.Platform + "_Include_Path";
        }

        /// <summary>
        /// Returns the library-path variable name for the configuration passed in.
        /// For example "Debug_Library_Path".
        /// </summary>
        private string getLibraryPathVariableName(ProjectConfigurationInfo_CPP configuration)
        {
            return configuration.Name + "_" + configuration.Platform + "_Library_Path";
        }

        /// <summary>
        /// Returns the libraries variable name for the configuration passed in.
        /// For example "Debug_Libraries".
        /// </summary>
        private string getLibrariesVariableName(ProjectConfigurationInfo_CPP configuration)
        {
            return configuration.Name + "_" + configuration.Platform + "_Libraries";
        }

        /// <summary>
        /// Returns the preprocessor-definitions variable name for the configuration passed in.
        /// For example "Debug_Preprocessor_Definitions".
        /// </summary>
        private string getPreprocessorDefinitionsVariableName(ProjectConfigurationInfo_CPP configuration)
        {
            return configuration.Name + "_" + configuration.Platform + "_Preprocessor_Definitions";
        }

        /// <summary>
        /// Returns the compiler-flags variable name for the configuration passed in.
        /// For example "Debug_Compiler_Flags".
        /// </summary>
        private string getCompilerFlagsVariableName(ProjectConfigurationInfo_CPP configuration)
        {
            return configuration.Name + "_" + configuration.Platform + "_Compiler_Flags";
        }

        /// <summary>
        /// Returns the linker-flags variable name for the configuration passed in.
        /// For example "Debug_Linker_Flags".
        /// </summary>
        private string getLinkerFlagsVariableName(ProjectConfigurationInfo_CPP configuration)
        {
            return configuration.Name + "_" + configuration.Platform + "_Linker_Flags";
        }

        /// <summary>
        /// Generate full path
        /// </summary>
        private string generateFullPath(string path)
        {
            return path.StartsWith(".") ? m_projectInfo.RootFolderAbsolute + path : path;
        }

        #endregion

        #region Private data

        // The parsed project data that we are creating the configure file from...
        private ProjectInfo_CPP m_projectInfo = null;

        // The configuration for this project
        MakeItSoConfig_Project m_projectConfig = null;

        // The file we write to...
        private StreamWriter m_file = null;

        #endregion
    }
}
