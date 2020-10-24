Receive raid notifications through the official Rust companion mobile app and never get offlined again!

Everyone who's on the TC list of authorized players will receive a notification when someone else (who's not on the list) destroy a wall, door etc.

## Usage

Make sure you're paired with the server to receive notifications.

Raid alarm is enabled by default, but can be disabled per user with `/raidalarm disable`.

## Chat Commands

* `/raidalarm` - Display some help information
* `/raidalarm test` -  Send a test raid alarm notification 
* `/raidalarm status` - Get the status of the raid alarm
* `/raidalarm enable/disable` - Enable/disable the raid alarm

## Permissions
 `raidalarm.use` - Permissions needs to be enabled in the config
 
## Configuration
```json
{
  "usePermissions": false
}
```

## Localization
