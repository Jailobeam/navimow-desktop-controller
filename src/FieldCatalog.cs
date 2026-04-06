using System.Collections.Generic;

namespace NavimowDesktopController
{
    internal static class FieldCatalog
    {
        private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
        {
            { "id", "Device ID" },
            { "device_id", "Device ID" },
            { "name", "Device Name" },
            { "model", "Model" },
            { "firmware_version", "Firmware Version" },
            { "serial_number", "Serial Number" },
            { "mac_address", "MAC Address" },
            { "online", "Online Status" },
            { "product_key", "Product Key" },
            { "productKey", "Product Key" },
            { "device_name", "Device Name" },
            { "deviceName", "Device Name" },
            { "iot_id", "IoT ID" },
            { "iotId", "IoT ID" },
            { "vehicleState", "Mower State" },
            { "status", "Status" },
            { "state", "State" },
            { "battery", "Battery Level (%)" },
            { "capacityRemaining", "Remaining Capacity" },
            { "rawValue", "Raw Value" },
            { "unit", "Unit" },
            { "position", "GPS Position" },
            { "latitude", "Latitude" },
            { "longitude", "Longitude" },
            { "lat", "Latitude" },
            { "lng", "Longitude" },
            { "error_code", "Error Code" },
            { "error_message", "Error Message" },
            { "errorCode", "Error Code" },
            { "signal_strength", "Signal Strength" },
            { "signalStrength", "Signal Strength" },
            { "mowing_time", "Current Mowing Time (s)" },
            { "total_mowing_time", "Total Mowing Time (s)" },
            { "timestamp", "Last Update Timestamp" },
            { "descriptiveCapacityRemaining", "Battery Description" },
            { "extra", "Extra Information" },
        };

        private static readonly Dictionary<string, Dictionary<string, string>> StateLabels = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "vehicleState",
                new Dictionary<string, string>
                {
                    { "isDocked", "Docked" },
                    { "isIdel", "Idle" },
                    { "isIdle", "Idle" },
                    { "isMapping", "Mapping" },
                    { "isRunning", "Mowing" },
                    { "isPaused", "Paused" },
                    { "isDocking", "Returning to Dock" },
                    { "Error", "Error" },
                    { "error", "Error" },
                    { "isLifted", "Lifted (Error)" },
                    { "inSoftwareUpdate", "Software Update" },
                    { "Self-Checking", "Self-Checking" },
                    { "Self-checking", "Self-Checking" },
                    { "Offline", "Offline" },
                    { "offline", "Offline" },
                }
            },
            {
                "status",
                new Dictionary<string, string>
                {
                    { "idle", "Idle" },
                    { "mowing", "Mowing" },
                    { "paused", "Paused" },
                    { "docked", "Docked" },
                    { "charging", "Charging" },
                    { "error", "Error" },
                    { "returning", "Returning to Dock" },
                    { "unknown", "Unknown" },
                }
            },
            {
                "state",
                new Dictionary<string, string>
                {
                    { "idle", "Idle" },
                    { "mowing", "Mowing" },
                    { "paused", "Paused" },
                    { "docked", "Docked" },
                    { "charging", "Charging" },
                    { "error", "Error" },
                    { "returning", "Returning to Dock" },
                    { "unknown", "Unknown" },
                }
            },
            {
                "error_code",
                new Dictionary<string, string>
                {
                    { "none", "No Error" },
                    { "stuck", "Stuck" },
                    { "lifted", "Lifted" },
                    { "rain", "Rain" },
                    { "battery_low", "Battery Low" },
                    { "sensor_error", "Sensor Error" },
                    { "motor_error", "Motor Error" },
                    { "blade_error", "Blade Error" },
                    { "unknown", "Unknown Error" },
                }
            },
        };

        public static string GetLabel(string key)
        {
            string value;
            return Descriptions.TryGetValue(key, out value) ? value : key;
        }

        public static string GetDisplayValue(string key, string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return "-";
            }

            Dictionary<string, string> labels;
            string mapped;
            if (StateLabels.TryGetValue(key, out labels) && labels.TryGetValue(rawValue, out mapped))
            {
                return mapped + " (" + rawValue + ")";
            }

            return rawValue;
        }
    }
}
