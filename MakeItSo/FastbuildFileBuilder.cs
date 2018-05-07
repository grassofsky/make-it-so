using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MakeItSoLib;

namespace MakeItSo
{
    /// <summary>
    /// Creates a Fastbuild configure file from the parsed solution passed to
    /// the constructor.
    /// </summary><remarks>
    /// This class creates a master configure file in the solution folder. This 
    /// has the standard 'fbuild.bff' name. 
    /// 
    /// We then create a configure file for each project in the solution in the
    /// project's own folder. These have the name '[project-name].bff'
    /// The project makefiles are created using FastbuildFileBuilder_Project objects.
    /// 
    /// The master configure just sets up the environment variable, and 
    /// dependencies between the projects in the solution, 
    /// and invokes each project's own configure file to build it.
    /// 
    /// </remarks>
    class FastbuildFileBuilder
    {
        #region Public methods and properties
        
        /// <summary>
        /// Creates a Fastbuild configure file for the solution passed in.
        /// </summary>
        public static void createConfigurationFile(SolutionInfo solution)
        {
            new FastbuildFileBuilder(solution);
        }

        #endregion

        #region Private functions

        /// <summary>
        /// Constructor
        /// </summary>
        private FastbuildFileBuilder(SolutionInfo solution)
        {
            m_solution = solution;

            // Create 'master' solution configure file
            createSolutionConfigureFile();

            // Create configure file for each project in the solution.
            createProjectConfigureFiles();
        }

        /// <summary>
        /// Create the 'master' solution configure file
        /// </summary>
        private void createSolutionConfigureFile()
        {
            try
            {
                string path = m_solution.RootFolderAbsolute + "/" + m_solution.Name + ".bff";
                m_file = new StreamWriter(path, false);
                m_file.NewLine = "\n";

                // Create common Setting
                createCommonSetting();

                // Create one target for each project
                createProjectTargets();

                // Create all projects root target
                createAllProjectsTarget();
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
        /// Create a configure file for each project in the solution.
        /// </summary>
        private void createProjectConfigureFiles()
        {
            foreach (ProjectInfo projectInfo in m_solution.getProjectInfos())
            {
                createProjectConfigureFile(projectInfo);
            }
        }

        private void createProjectConfigureFile(ProjectInfo projectInfo)
        {
            // Are we ignoring this project?
            if (MakeItSoConfig.Instance.ignoreProject(projectInfo.Name) == true)
            {
                return;
            }

            // We build a different makefile, depending on the
            // project type...
            if (projectInfo is ProjectInfo_CPP)
            {
                FastbuildFileBuilder_Project_CPP.createConfigurationFile(projectInfo as ProjectInfo_CPP);
            }
            if (projectInfo is ProjectInfo_CSharp)
            {
                FastbuildFileBuilder_Project_CSharp.createConfigurationFile(projectInfo as ProjectInfo_CSharp);
            }
        }

        /// <summary>
        /// Create Common setting, such as Compiler, Setting, and so on
        /// </summary>
        private void createCommonSetting()
        {
            // TODO: add the variable to environment variable
            m_file.WriteLine(".VSBasePath         = 'C:\\Program Files (x86)\\Microsoft Visual Studio 10.0'");
            m_file.WriteLine(".CSBasePath         = 'C:\\Windows\\Microsoft.NET'");
            m_file.WriteLine(".WindowsSDKBasePath = 'C:\\Program Files (x86)\\Microsoft SDKs\\Windows\\v7.0A'");
            m_file.WriteLine("");

            m_file.WriteLine("Settings");
            m_file.WriteLine("{");
            m_file.WriteLine("  .Environment = {");
            m_file.WriteLine("     'PATH=$VSBasePath$\\Common7\\IDE;$VSBasePath$\\VC\\bin;$VSBasePath$\\VC\\bin\\amd64\\'");
            m_file.WriteLine("     'TMP=C:\\Windows\\Temp'");
            m_file.WriteLine("     'SystemRoot=C:\\Windows'");
            m_file.WriteLine("  }");
            m_file.WriteLine("}");
            m_file.WriteLine("");

            m_file.WriteLine("// CSharp Compiler");
            m_file.WriteLine(".MSCSBaseConfig =");
            m_file.WriteLine("[");
            m_file.WriteLine("  .CompilerOptions = ' /out:\"%2\"'");
            m_file.WriteLine("                   + ' /reference:\"%3\"'");
            m_file.WriteLine("]");
            m_file.WriteLine("");
            m_file.WriteLine(".csx86BaseConfig =");
            m_file.WriteLine("[");
            m_file.WriteLine("  Using( .MSCSBaseConfig )");
            m_file.WriteLine("  .Compiler = '$CSBasePath$\\Framework\\v4.0.30319\\csc.exe'");
            m_file.WriteLine("]");
            m_file.WriteLine("");
            m_file.WriteLine(".csx64BaseConfig =");
            m_file.WriteLine("[");
            m_file.WriteLine("  Using( .MSCSBaseConfig )");
            m_file.WriteLine("  .Compiler = '$CSBasePath$\\Framework64\\v4.0.30319\\csc.exe'");
            m_file.WriteLine("]");
            m_file.WriteLine("");

            m_file.WriteLine("// Compilers");
            m_file.WriteLine("Compiler('Compiler-x86')");
            m_file.WriteLine("{");
            m_file.WriteLine("  .Root       = '$VSBasePath$\\VC\\bin'");
            m_file.WriteLine("  .Executable = '$Root$\\cl.exe'");
            m_file.WriteLine("  .ExtraFiles = { '$Root$\\c1.dll'");
            m_file.WriteLine("                  '$Root$\\c1ast.dll'");
            m_file.WriteLine("                  '$Root$\\c1xx.dll'");
            m_file.WriteLine("                  '$Root$\\c1xxast.dll'");
            m_file.WriteLine("                  '$Root$\\c2.dll'");
            m_file.WriteLine("                  '$Root$\\mspft80.dll'");
            m_file.WriteLine("                  '$Root$\\pgodb100.dll'");
            m_file.WriteLine("                  '$Root$\\pgort100.dll'");
            m_file.WriteLine("                  '$Root$\\1033\\clui.dll'");
            m_file.WriteLine("                  '$VSBasePath$\\VC\\redist\\x86\\Microsoft.VC100.CRT\\msvcp100.dll'");
            m_file.WriteLine("                  '$VSBasePath$\\VC\\redist\\x86\\Microsoft.VC100.CRT\\msvcr100.dll'");
            m_file.WriteLine("                  '$VSBasePath$\\VC\\redist\\x86\\Microsoft.VC100.MFC\\mfc100.dll'");
            m_file.WriteLine("                  '$VSBasePath$\\VC\\redist\\x86\\Microsoft.VC100.MFC\\mfc100u.dll'");
            m_file.WriteLine("                  '$VSBasePath$\\VC\\redist\\x86\\Microsoft.VC100.MFC\\mfcm100.dll'");
            m_file.WriteLine("                  '$VSBasePath$\\VC\\redist\\x86\\Microsoft.VC100.MFC\\mfcm100u.dll'");
            m_file.WriteLine("                }");
            m_file.WriteLine("}");
            m_file.WriteLine("");
            m_file.WriteLine("Compiler('Compiler-x64')");
            m_file.WriteLine("{");
            m_file.WriteLine("  .Root       = '$VSBasePath$\\VC\\bin\\amd64'");
            m_file.WriteLine("  .Executable = '$Root$\\cl.exe'");
            m_file.WriteLine("  .ExtraFiles = { '$Root$\\c1.dll'");
            m_file.WriteLine("                  '$Root$\\c1xx.dll'");
            m_file.WriteLine("                  '$Root$\\c2.dll'");
            m_file.WriteLine("                  '$Root$\\pgodb100.dll'");
            m_file.WriteLine("                  '$Root$\\pgort100.dll'");
            m_file.WriteLine("                  '$Root$\\mspdb100.dll'");
            m_file.WriteLine("                  '$Root$\\msobj100.dll'");
            m_file.WriteLine("                  '$Root$\\mspdbcore.dll'");
            m_file.WriteLine("                  '$Root$\\1033\\clui.dll'");
            m_file.WriteLine("                  '$VSBasePath$\\Common7\\IDE\\mspdbsrv.exe'");
            m_file.WriteLine("                  '$VSBasePath$\\VC\\redist\\x64\\Microsoft.VC100.CRT\\msvcp100.dll'");
            m_file.WriteLine("                  '$VSBasePath$\\VC\\redist\\x64\\Microsoft.VC100.CRT\\msvcr100.dll'");
            m_file.WriteLine("                  '$VSBasePath$\\VC\\redist\\x64\\Microsoft.VC100.MFC\\mfc100.dll'");
            m_file.WriteLine("                  '$VSBasePath$\\VC\\redist\\x64\\Microsoft.VC100.MFC\\mfc100u.dll'");
            m_file.WriteLine("                  '$VSBasePath$\\VC\\redist\\x64\\Microsoft.VC100.MFC\\mfcm100.dll'");
            m_file.WriteLine("                  '$VSBasePath$\\VC\\redist\\x64\\Microsoft.VC100.MFC\\mfcm100u.dll'");
            m_file.WriteLine("                }");
            m_file.WriteLine("}");
            m_file.WriteLine("");

            m_file.WriteLine("// Configurations");
            m_file.WriteLine(".MSVCBaseConfig = ");
            m_file.WriteLine("[");
            m_file.WriteLine("  .CompilerOptions  = '\"%1\"'");
            m_file.WriteLine("                    + ' /Fo\"%2\"'");
            m_file.WriteLine("                    + ' /I\"$VSBasePath$/VC/include\"'");
            m_file.WriteLine("                    + ' /I\"$VSBasePath$/VC/atlmfc/include\"'");
            m_file.WriteLine("                    + ' /I\"$WindowsSDKBasePath$/Include\"'");
            m_file.WriteLine("");
            m_file.WriteLine("  .LinkerOptions    = ' /OUT:\"%2\"'");
            m_file.WriteLine("                    + ' \"%1\"'");
            m_file.WriteLine("");
            m_file.WriteLine("  .LibrarianOptions = '\"%1\"'");
            m_file.WriteLine("                    + ' /OUT:\"%2\"'");
            m_file.WriteLine("                    + ' /nologo'");
            m_file.WriteLine("]");
            m_file.WriteLine("");

            m_file.WriteLine(".x86BaseConfig = ");
            m_file.WriteLine("[");
            m_file.WriteLine("  Using( .MSVCBaseConfig )");
            m_file.WriteLine("  .ToolsBasePath   = '$VSBasePath$\\VC\\bin'");
            m_file.WriteLine("  .Compiler        = 'Compiler-x86'");
            m_file.WriteLine("  .Librarian       = '$ToolsBasePath$\\lib.exe'");
            m_file.WriteLine("  .Linker          = '$ToolsBasePath$\\link.exe'");
            m_file.WriteLine("  .LinkerOptions   + ' /LIBPATH:\"$VSBasePath$\\VC\\lib\"'");
            m_file.WriteLine("                   + ' /LIBPATH:\"$VSBasePath$\\VC\\atlmfc\\lib\"'");
            m_file.WriteLine("                   + ' /LIBPATH:\"$WindowsSDKBasePath$\\Lib\"'");
            m_file.WriteLine("]");
            m_file.WriteLine("");

            m_file.WriteLine(".x64BaseConfig = ");
            m_file.WriteLine("[");
            m_file.WriteLine("  Using( .MSVCBaseConfig )");
            m_file.WriteLine("  .ToolsBasePath   = '$VSBasePath$\\VC\\bin\\amd64'");
            m_file.WriteLine("  .Compiler        = 'Compiler-x64'");
            m_file.WriteLine("  .Librarian       = '$ToolsBasePath$\\lib.exe'");
            m_file.WriteLine("  .Linker          = '$ToolsBasePath$\\link.exe'");
            m_file.WriteLine("  .LinkerOptions   + ' /LIBPATH:\"$VSBasePath$\\VC\\lib\\amd64\"'");
            m_file.WriteLine("                   + ' /LIBPATH:\"$VSBasePath$\\VC\\lib\"'");
            m_file.WriteLine("                   + ' /LIBPATH:\"$VSBasePath$\\VC\\atlmfc\\lib\\amd64\"'");
            m_file.WriteLine("                   + ' /LIBPATH:\"$WindowsSDKBasePath$\\Lib\\x64\"'");
            m_file.WriteLine("]");
            m_file.WriteLine("");
        }

        /// <summary>
        /// Process project's dependences in waitedForProcess List
        /// </summary>
        private void ProcessingDependences(ProjectInfo projectInfo, ref List<ProjectInfo> waitedForProcess)
        {
            if (waitedForProcess.IndexOf(projectInfo) == -1)
            {
                return;
            }

            if (projectInfo.ProjectType != ProjectInfo.ProjectTypeEnum.INVALID &&
                MakeItSoConfig.Instance.ignoreProject(projectInfo.Name) == false)
            {
                foreach (ProjectInfo depProjectInfo in projectInfo.getRequiredProjects())
                {
                    ProcessingDependences(depProjectInfo, ref waitedForProcess);
                    ProjectPriority.AddDependency(projectInfo, depProjectInfo);
                }
                waitedForProcess.Remove(projectInfo);
            }
        }

        /// <summary>
        /// Include individual bff configuration file for each project
        /// </summary>
        private void createProjectTargets()
        {
            /// Here the dependencies of the projects are considered.
            /// Step 1. Store all project config path to ProjectPriority dictionary
            List<ProjectInfo> waitedForProcess = new List<ProjectInfo>();
            foreach (ProjectInfo projectInfo in m_solution.getProjectInfos())
            {
                if (projectInfo.ProjectType != ProjectInfo.ProjectTypeEnum.INVALID &&
                    MakeItSoConfig.Instance.ignoreProject(projectInfo.Name) == false)
                {
                    ProjectPriority.AddProjectPriority(projectInfo);
                    waitedForProcess.Add(projectInfo);
                }
            }

            /// Step 2. Add all dependencies of every project
            while (waitedForProcess.Count != 0)
            {
                var projectInfo = waitedForProcess[0];
                ProcessingDependences(projectInfo, ref waitedForProcess);
            }

            /// Step 3. Get all sorted project configuration file path
            var sortedProjectConfigPath = ProjectPriority.GetSortedProjectConfigPath();

            foreach (string configPath in sortedProjectConfigPath)
            {
                m_file.WriteLine("#include \"" + configPath + "\"");
            }
            m_file.WriteLine("");
        }

        /// <summary>
        /// Writes an 'all' target that depends on all the 
        /// individual projects.
        /// </summary>
        private void createAllProjectsTarget()
        {
            // We create a target like:
            //   Alias('All') 
            //   { 
            //      .Targets = { 'project1', 'project2' } 
            //   }
            //
            m_file.WriteLine("// Builds all the projects in the solution...");
            m_file.WriteLine("Alias('All')");
            m_file.WriteLine("{");

            string target = "  .Targets = { ";
            foreach (ProjectInfo projectInfo in m_solution.getProjectInfos())
            {
                if (MakeItSoConfig.Instance.ignoreProject(projectInfo.Name) == false)
                {
                    if (projectInfo.ProjectType != ProjectInfo.ProjectTypeEnum.INVALID)
                    {
                        target += ("'" + projectInfo.Name + "', ");
                    }
                }
            }
            target += "}";

            m_file.WriteLine(target);
            m_file.WriteLine("}");
            m_file.WriteLine("");
        }

        #endregion

        #region Private data

        private class ProjectPriorityManager
        {


        }

        /// <summary>
        /// To store project priority, this class is private in FastbuildFileBuilder
        /// </summary>
        private class ProjectPriority
        {
            #region Public 
            /// <summary>
            /// Initialize project priority dictionary
            /// </summary>
            public static void AddProjectPriority(ProjectInfo projectInfo)
            {
                m_ProjDict.Add(projectInfo.Name, new ProjectPriority(projectInfo));
            }

            /// <summary>
            /// Add dependent project of source project
            /// </summary>
            public static void AddDependency(ProjectInfo srcProjectInfo, ProjectInfo depProjectInfo)
            {
                m_ProjDict[srcProjectInfo.Name].addDependency(depProjectInfo.Name);
            }

            /// <summary>
            /// Get the sorted project configuration path
            /// </summary>
            public static List<string> GetSortedProjectConfigPath()
            {
                List<ProjectPriority> projList = new List<ProjectPriority>();
                foreach (KeyValuePair<string, ProjectPriority> entry in m_ProjDict)
                {
                    projList.Add(entry.Value);
                }
                projList.Sort((i1, i2) => (i2.m_priority.CompareTo(i1.m_priority)));

                List<string> sortedList = new List<string>();
                foreach (ProjectPriority proj in projList)
                {
                    sortedList.Add(proj.m_projectInfo.RootFolderRelative + proj.m_projectInfo.Name + ".bff");
                }
                return sortedList;
            }

            #endregion

            #region Private function

            /// <summary>
            /// Constructor
            /// </summary>
            private ProjectPriority(ProjectInfo projectInfo)
            {
                m_projectInfo = projectInfo;
                m_priority = 0;
            }

            /// <summary>
            /// Add the dependent project configuration path
            /// </summary>
            private void addDependency(string key)
            {
                dependences.Add(key);
                m_ProjDict[key].increasePriority();
            }

            /// <summary>
            /// Increase priority of the project
            /// </summary>
            private void increasePriority()
            {
                m_ProjDict[m_projectInfo.Name].m_priority++;
                foreach (string key in dependences)
                {
                    m_ProjDict[key].increasePriority();
                }
            }

            #endregion

            #region Private data
            // Project Info
            private ProjectInfo m_projectInfo;

            // priority start from 0
            private int m_priority;

            // dependence project name
            private List<string> dependences = new List<string>();

            // Store all project
            private static Dictionary<string, ProjectPriority> m_ProjDict = new Dictionary<string, ProjectPriority>();

            #endregion
        }

        // The parsed solution data.
        private SolutionInfo m_solution = null;

        // The file for the 'master' configure file.
        private StreamWriter m_file = null;

        #endregion
    }
}
