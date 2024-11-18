
# PPSA Project
================

## Overview
--------

The PPSA (PLC - PC Shutdown Automation) project is a software solution designed to automate and monitor industrial power systems. The project utilizes C# and the .NET Framework to create a robust and scalable application.

## Features
--------

* Automated shutdown
* Omron Soft-NA shutdown process
* Real-time monitoring of PLC tag
* Folder cleanup
* Error handling and logging using NLog
* Support for multiple PLC configurations and folder paths

## Requirements
------------

* .NET Framework 4.7.2 or later
* [libplctag.NET](https://github.com/libplctag/libplctag.NET) library for PLC communication
* NLog library for logging and error handling
* System.Configuration.ConfigurationManager library for configuration management

## Configuration
-------------

The project uses an App.config file to store configuration settings. The following settings are available:

* PLCTagName: The name of the PLC tag to monitor
* PlcGateway: The IP address of the PLC gateway
* PlcPath: The path to the PLC device
* PlcReadInterval: The interval at which to read the PLC tag
* PlcTimeout: The timeout for PLC communication
* ProgramToClose: The name of the program to close during shutdown
* ShutdownGracePeriod: The time period to wait before shutting down the system
* ProcessCloseTimeout: The time period to wait before forcing a process to close
* MaxFolderCount: The maximum number of folders to keep in the cleanup directory
* DaysThreshold: The number of days to keep folders in the cleanup directory
* FolderPaths: A semicolon-separated list of folder paths to clean up

## Usage
-----

1. Build the project using Visual Studio or the .NET CLI.
2. Configure the App.config file with the desired settings.
3. Run the application using the .NET CLI or by double-clicking the executable.

## Contributing
------------

Contributions are welcome! If you would like to contribute to the project, please fork the repository and submit a pull request with your changes.

## License
-------

This project is licensed under the Unlicense. See the LICENSE.txt file for more information.

## Acknowledgments
---------------

* [libplctag.NET](https://github.com/libplctag/libplctag.NET) library for PLC communication
* NLog library for logging and error handling
* System.Configuration.ConfigurationManager library for configuration management
