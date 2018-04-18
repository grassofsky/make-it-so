using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MakeItSoLib;

namespace MakeItSo
{
    /// <summary>
    /// Creates a configure file from the parsed solution
    /// </summary><remarks>
    /// 
    /// The configure file can be:
    /// - Linux makefile
    /// - Fastbuild configure file
    /// - Cmakelist
    /// - Scons configure file
    /// 
    /// </remarks>
    class ConfigureFileBuilder
    {
        #region Public methods and properties
        public enum ConfigureFileType
        {
            Makefile,
            Fastbuild,
            CMakeList,
            Scons
        }

        public static void createConfigurationFile(SolutionInfo solution, ConfigureFileType type)
        {
            switch (type)
            {
                case ConfigureFileType.Fastbuild:
                    FastbuildFileBuilder.createConfigurationFile(solution);
                    break;
            }
        }
        #endregion
    }
}
