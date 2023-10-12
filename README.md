# WorkingTitle2GSX

- GSX / SimBrief Integration for WorkingTitle 787 Aircrafts
- Only a basic Payload & Fuel GSX Integration, not a single Automation

Download under [Releases](https://github.com/Fragtality/WorkingTitle2GSX/releases)

<br/><br/>

## Requirements

- Any WT based 787 (Stock, Kuro/Horizon, 4Simmers)
- [FSUIPC7](http://fsuipc.com/) latest installed (a registered Copy is not required, I think)
- GSX Pro
- SimBrief Account
- [.NET 7 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) (.NET Runtime and .NET Desktop Runtime. Do not confuse it with arm64! Reboot may be required after first Installation)

<br/>

## Installation

- Extract to a reasonable Folder
- AV Exclusions may be required
- Run as Admin is NOT required
- Set your SimBrief PilotID in the GUI

<br/>

## Usage

- The Aircraft Type in SimBrief and the Simulator must match
- Start the Binary before MSFS or in the Main Menu
- The Aircraft will shortly "shake" because it is reset to Empty on Startup (only when on Ground!)
- As soon as you switch on the Batteries the Binary will import Fuel & Payload from SimBrief
- Call GSX Refuel to fuel the Aircraft to the planned SimBrief Figures. Start the APU only after Refuel was completed.
- Call GSX Boarding to board Passengers and load Cargo/Bag as planned in SimBrief
- When arrived, call GSX Deboarding to unload the Plane
- When Deboarding was finished, SimBrief will be checked for new FlightPlan. If none is found, it will re-check every 60 Seconds. If a new FlightPlan is found, you can call Refueling/Boarding again.

<br/>

## Options

Use the GUI for Configuration.

<br/>

## NOTAM

- This will NOT be a second Fenix2GSX for the Dreamliner
- It is Work in Progress and an "interim-Solution" at Max
