# Overview
MakeItSo converts Visual Studio solutions to:
- gcc makefiles for Linux (TODO)
- fastbuild configuration file (TODO)
- CMake file (TODO)
- Scons configure file (TODO)

It will convert all projects in a solution and create a configure file for each one. It also creates a master configure file that will build them in the correct dependency order.

## fastbuild configuration file

To support fastbuild:

1. vcproject-property-Linker-Additional Dependencies should not have inherited values, all the value in "inherited values" should be added to additional dependencies.

# Other Information

See original [readme](https://github.com/stupidgrass/make-it-so/blob/master/README_original.markdown)
