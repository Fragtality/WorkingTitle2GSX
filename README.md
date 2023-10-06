# WorkingTitle2GSX

- GSX / SimBrief Integration for WorkingTitle 787 Aircrafts
- Only a basic Payload & Fuel GSX Integration, not a single Automation

<br/><br/>

## Requirements

- Any WT based 787 (Stock, Kuro/Horizon, 4Simmers)
- FSUIPC7 latest installed (a registered Copy is NOT required)
- GSX Pro
- SimBrief Account
- .NET Framework 4.8 (should be preinstalled on Win10/11)

<br/>

## Installation

- Extract to a reasonable Folder
- AV Exclusions may be required
- Run as Admin is NOT required
- Set your SimBrief PilotID in the .config File

<br/>

## Usage

- Start the Binary AFTER you hit "Ready to Fly"
- The Aircraft will shortly "shake" because it is reset to Empty on Startup (only when on Ground!)
- As soon as you switch on the Batteries the Binary will import Fuel & Payload from SimBrief
- Call GSX Refuel to fuel the Aircraft to the planned SimBrief Figures. Start the APU only after Refuel was completed.
- Call GSX Boarding to board Passengers and load Cargo/Bag as planned in SimBrief
- When arrived, call GSX Deboarding to unload the Plane
- After Deboarding, SimBrief will be checked every 60s for a new FlightPlan. If a new FlightPlan is found, you can call Refueling/Boarding

<br/>

## Options

All Options are stored in the `WorkingTitle2GSX.exe.config` File. Only touch the Options below, DO NOT touch anything else.

- **pilotID**: Your (numerical) PilotID in SimBrief. Required for the Tool to work at all.
- **useActualValue**: SimBrief has a light "Randomization" built for Passenger and Bag Count (the "actual" Value).
- **noCrewBoarding**: Disables Crew Boarding and Deboarding: GSX will not ask, Crew is set to 3 Pilots & 9 Flight Attendants.
- **gallonsPerSecond**: The Refuel-Speed in Gallons per Second.
- **startFuelWingPercent**: The Percentage of Fuel to set in each Wing-Tank on Startup (Center will always be 0).
- **distPaxPercent**: The Distribution of Passengers (as Percentage) across the Different Classes: Business, Premium Economy and Economy.
- **distCargoPercent**: The Distribution of Cargo/Bag (as Percentage) between the Forward and Aft Cargo Bay. 

<br/>

## NOTAM

- This will NOT be a second Fenix2GSX for the Dreamliner
- It is Work in Progress and an "interim-Solution" at Max
