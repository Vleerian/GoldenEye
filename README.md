# GoldenEye
Vleerian's NS Region diagnostics tool

# Usage
`./GoldenEye (your main nation) (region) --no-compare `
Will return a report with useful data about the current state of the region, including:
- If the region's delegate is executive (raidable)
- If the region is founderless
- How many nations are in the region
- How many embassieds the region has
- How many Regional Officers the region has
- A readout of the delegate and ROs displaying:
    - Regional influence level
    - If they have border control permissions
    - If they are in the World Assembly
    - How many endorsements they have
    - How much SPDR (Influence) they have
    - If they have the ability to impose a password (Visible, then Invisible)

`./GoldenEye (your main nation) (region) (data dump file)`
Will compare the current state of the region with the provided data dump, returning all the same data as above along with Net change values

`./GoldenEye (your main nation) (region) --cds`
will fetch the Civil Defence Siren dispatch, save a local copy, then compare the region's embassy list to see if has embassies listed in the Civil Defence Siren dispatch

`./GoldenEye (your main nation) (region) --full-report`
will download and save local copies of all nations in the region, then output CSV files containing data on nations, partitioning them into the following groups:
- Delegate and ROs
- Nations endorsing the Delegate
- Nations endorsing the ROs
- Non WA nations
these include additional information not shown on the main window, namely residency.

`./GoldenEye (your main nation) (region) --full-report --defender-points "pointnation1,point_nation2,point nation3"`
Will generate additional CSV files for the following groups
- The specified point nation
- nations endorsing the specified nations

# Running GoldenEye

It is suggested you run one of the [url=https://github.com/Vleerian/GoldenEye/latest]Latest Releases[/url]

To run from source you will need the DotNet6 SDK, and run the build command
`dotnet build -C Release`

## Disclaimer

The software is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied
warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

Any individual using a script-based NationStates API tool is responsible for ensuring it complies with the latest
version of the [API Terms of Use](https://www.nationstates.net/pages/api.html#terms). GoldenEye is designed to comply with
these rules, including the rate limit, as of April 12th 2022, under reasonable use conditions, but the authors are not
responsible for any unintended or erroneous program behavior that breaks these rules.

Never run more than one program that uses the NationStates API at once. Doing so will likely cause your IP address to
exceed the API rate limit, which will in turn cause both programs to fail.
