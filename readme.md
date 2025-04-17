<p align="center"><img width=60% src="docs/header.png"></p>

> Automated generation of Apple's iCloud emails via HideMyEmail.

_You do need to have an active iCloud+ subscription to be able to generate iCloud emails..._

<p align="center"><img src="docs/example.png"></p>

# iCloud HideMyEmail Generator

iCloud Hide My Email address generator written in C#.

## Requirements

- .NET 9.0 SDK
- iCloud account with Hide My Email feature enabled
- iCloud authentication cookie file

## Installation

1. Clone the repository
2. Navigate to the project directory
3. Build the project:

```shell
dotnet build
```

## Configuration

1. Create a `cookie.txt` file in the project root directory
2. Copy your iCloud cookie to the file (you can use browser developer tools)
   - See the example in `cookie.example.txt`

### How to obtain cookie.txt


1. Download [EditThisCookie](https://chrome.google.com/webstore/detail/editthiscookie/fngmhnnpilhplaeedifhccceomclgfbg) Chrome extension

2. Go to [EditThisCookie settings page](chrome-extension://fngmhnnpilhplaeedifhccceomclgfbg/options_pages/user_preferences.html) and set the preferred export format to `Semicolon separated name=value pairs`

<p align="center"><img src="docs/cookie-settings.png" width=70%></p>

3. Navigate to [iCloud settings](https://www.icloud.com/settings/) in your browser and log in

4. Click on the EditThisCookie extension and export cookies

<p align="center"><img src="docs/export-cookies.png" width=70%></p>

5. Paste the exported cookies into a file named `cookie.txt`

## Usage

### Generating Email Addresses

To generate the default 5 email addresses:

```shell
dotnet run
```

or

```shell
dotnet run -- generate
```

To generate a specific number of email addresses:

```shell
dotnet run -- generate --count 10
```

### Displaying Email Addresses

To display all active email addresses:

```shell
dotnet run -- list
```

To display inactive email addresses:

```shell
dotnet run -- list --inactive
```

To search for email addresses using a regular expression:

```shell
dotnet run -- list --search "pattern"
```

## How It Works

The program uses the official iCloud API to generate and manage Hide My Email addresses:

1. Authentication using a cookie file
2. Generating email addresses through the iCloud API
3. Reserving the generated addresses
4. Saving the generated addresses to an `emails.txt` file

## License

This project is licensed under the MIT License. See the LICENSE file for more information.