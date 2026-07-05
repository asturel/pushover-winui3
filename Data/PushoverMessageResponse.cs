using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using PushoverDesktopClient.Converters;

namespace PushoverDesktopClient;

/// <summary>
/// Root response object returned by Pushover API endpoints (e.g., /1/messages.json, /1/sounds.json).
/// </summary>
public class PushoverMessageResponse
{
    /// <summary>
    /// API execution status. Returns 1 if the request was valid and successfully queued, 
    /// or 0 (or other values) if any input parameter was invalid or an error occurred.
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>
    /// A randomly-generated unique token associated with this specific API request. 
    /// Used as a reference identifier when contacting Pushover support or checking backend logs.
    /// </summary>
    [JsonPropertyName("request")]
    public string Request { get; set; } = string.Empty;

    /// <summary>
    /// A collection of individual notification messages available for downloading/syncing.
    /// Kept lazily evaluated as JsonElement to retain exact raw JSON properties for specialized downstream storage.
    /// </summary>
    [JsonPropertyName("messages")]
    public List<JsonElement> Messages { get; set; } = [];

    /// <summary>
    /// Optional metadata describing the active user account domain, status, and licensing rules.
    /// </summary>
    [JsonPropertyName("user")]
    public PushoverUser? User { get; set; }

    /// <summary>
    /// Optional configuration envelope details outlining the target destination device profile.
    /// </summary>
    [JsonPropertyName("device")]
    public PushoverDevice? Device { get; set; }

    /// <summary>
    /// A string collection of native/custom alert sounds allocated to the current user configuration context.
    /// </summary>
    [JsonPropertyName("sounds")]
    public List<string> Sounds { get; set; } = [];

    /// <summary>
    /// A unique tracking identifier populated exclusively for Emergency Priority (2) notifications. 
    /// Can be queried against the receipts API to track user acknowledgment or callbacks.
    /// </summary>
    [JsonPropertyName("receipt")]
    public string? Receipt { get; set; }

    /// <summary>
    /// An array of descriptive verification error strings detailing parameter constraints, 
    /// quota limits, or invalid tokens when Status evaluates to 0.
    /// </summary>
    [JsonPropertyName("errors")]
    public List<string>? Errors { get; set; }
}

/// <summary>
/// Represents an individual notification message payload with metadata boundaries.
/// </summary>
public class PushoverMessageItem
{
    /// <summary>
    /// Unique 64-bit integer identifier assigned to this specific message transaction.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// String representation of the unique message identifier.
    /// </summary>
    [JsonPropertyName("id_str")]
    public string IdStr { get; set; } = string.Empty;

    /// <summary>
    /// The core textual body payload of the notification. Maximum boundary limit of 1024 UTF-8 characters.
    /// Can contain parsed HTML tags if HTML routing flags are evaluated.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The user-facing display name of the application or integration that dispatched this notification.
    /// </summary>
    [JsonPropertyName("app")]
    public string Application { get; set; } = string.Empty;

    /// <summary>
    /// Unique application ID mapping to the specific calling integration scope.
    /// </summary>
    [JsonPropertyName("aid")]
    public long Aid { get; set; }

    /// <summary>
    /// String representation of the unique application ID.
    /// </summary>
    [JsonPropertyName("aid_str")]
    public string AidStr { get; set; } = string.Empty;

    /// <summary>
    /// Identifier or key token resolving to the specific custom icon graphics displayed alongside the message.
    /// </summary>
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// The exact timestamp indicating when the Pushover API servers originally received and stored the notification payload.
    /// </summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset Date { get; set; }

    /// <summary>
    /// Presentation priority boundary parameter. 
    /// Supported values: -2 (Lowest, no alert), -1 (Low, quiet alert), 0 (Normal), 1 (High, bypass quiet hours), 2 (Emergency, recurring retries).
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    /// <summary>
    /// Binary representation flag indicating whether an Emergency Priority (2) message notification has been acknowledged by a user.
    /// </summary>
    [JsonPropertyName("acked")]
    public int Acked { get; set; }

    /// <summary>
    /// Unique user message identifier tracking transmission scope.
    /// </summary>
    [JsonPropertyName("umid")]
    public long Umid { get; set; }

    /// <summary>
    /// String representation of the unique user message identifier.
    /// </summary>
    [JsonPropertyName("umid_str")]
    public string UmidStr { get; set; } = string.Empty;

    /// <summary>
    /// Optional user-defined message title header. Maximum boundary limit of 250 characters. 
    /// If null, the application name is displayed by default.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Optional supplementary clickable hypermedia URL appended below the text frame. Maximum boundary limit of 512 characters.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Optional descriptive text header mapped over the supplemental URL hyperlink. Maximum boundary limit of 100 characters.
    /// </summary>
    [JsonPropertyName("url_title")]
    public string? UrlTitle { get; set; }

    /// <summary>
    /// Optional numeric reference code indicating a shared subscription group source profile identifier.
    /// </summary>
    [JsonPropertyName("subscription")]
    public long? Subscription { get; set; }

    /// <summary>
    /// String representation of the optional subscription group identifier.
    /// </summary>
    [JsonPropertyName("subscription_str")]
    public string? SubscriptionStr { get; set; }

    /// <summary>
    /// Server timestamp marking when the incoming request was parsed and appended to the transmission queue buffer.
    /// </summary>
    [JsonPropertyName("queued_date")]
    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset QueuedDate { get; set; }

    /// <summary>
    /// Server timestamp marking when the payload was actively pushed toward external notification gateways (APNs/FCM).
    /// </summary>
    [JsonPropertyName("dispatched_date")]
    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset DispatchedDate { get; set; }

    /// <summary>
    /// Optional timestamp tracking a Time to Live (TTL) parameters constraint. 
    /// Specifies when the target client endpoints must automatically delete the local notification cache record.
    /// </summary>
    [JsonPropertyName("expiration_date")]
    [JsonConverter(typeof(NullableUnixSecondsConverter))]
    public DateTimeOffset? ExpirationDate { get; set; }
}

/// <summary>
/// Metadata profile regarding the active user account configuration scope.
/// </summary>
public class PushoverUser
{
    /// <summary>
    /// Indicates whether the user currently has active "Quiet Hours" parameters enabled on their notification preferences.
    /// </summary>
    [JsonPropertyName("quiet_hours")]
    public bool QuietHours { get; set; }

    /// <summary>
    /// Indicates if the user account holds a valid ecosystem license authorization profile for Android platforms.
    /// </summary>
    [JsonPropertyName("is_android_licensed")]
    public bool IsAndroidLicensed { get; set; }

    /// <summary>
    /// Indicates if the user account holds a valid ecosystem license authorization profile for iOS/iPadOS platforms.
    /// </summary>
    [JsonPropertyName("is_ios_licensed")]
    public bool IsIosLicensed { get; set; }

    /// <summary>
    /// Indicates if the user account holds a valid ecosystem license authorization profile for Desktop client platforms.
    /// </summary>
    [JsonPropertyName("is_desktop_licensed")]
    public bool IsDesktopLicensed { get; set; }

    /// <summary>
    /// Primary registration email address associated with the active account scope.
    /// </summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp marking the historical epoch initialization point when the user account was first provisioned.
    /// </summary>
    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(UnixSecondsConverter))]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Primary incoming routing email address alias allocated to this user profile context.
    /// </summary>
    [JsonPropertyName("first_email_alias")]
    public string FirstEmailAlias { get; set; } = string.Empty;

    /// <summary>
    /// UI configuration parameter flag adjusting target tip-jar visualization visibility properties.
    /// </summary>
    [JsonPropertyName("show_tipjar")]
    public string ShowTipjar { get; set; } = string.Empty;

    /// <summary>
    /// UI configuration parameter flag adjusting target corporate organizational team promo visibility structures.
    /// </summary>
    [JsonPropertyName("show_team_ad")]
    public string ShowTeamAd { get; set; } = string.Empty;
}

/// <summary>
/// Metadata profile for the connected target endpoint device.
/// </summary>
public class PushoverDevice
{
    /// <summary>
    /// The user-defined alpha-numeric naming identifier representing this specific device profile registration.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unique internal encryption identifier tag linked to this endpoint's transport channel configuration boundaries.
    /// </summary>
    [JsonPropertyName("eid")]
    public string Eid { get; set; } = string.Empty;

    /// <summary>
    /// Specifies if End-to-End Encryption (E2EE AES-256-CBC) parsing sequences are explicitly active for this terminal connection.
    /// </summary>
    [JsonPropertyName("encryption_enabled")]
    public bool EncryptionEnabled { get; set; }

    /// <summary>
    /// The key identifier representing the fallback alert sound chosen by the user for standard incoming notifications.
    /// </summary>
    [JsonPropertyName("default_sound")]
    public string DefaultSound { get; set; } = string.Empty;

    /// <summary>
    /// Enforcement flag indicating if the user overrides individual application audio payloads with their absolute DefaultSound choice.
    /// </summary>
    [JsonPropertyName("always_use_default_sound")]
    public bool AlwaysUseDefaultSound { get; set; }

    /// <summary>
    /// The key identifier representing the alert tone assigned by default to incoming High/Emergency priority items.
    /// </summary>
    [JsonPropertyName("default_high_priority_sound")]
    public string DefaultHighPrioritySound { get; set; } = string.Empty;

    /// <summary>
    /// Enforcement flag indicating if the user overrides all application high priority sound variables with their target default choice.
    /// </summary>
    [JsonPropertyName("always_use_default_high_priority_sound")]
    public bool AlwaysUseDefaultHighPrioritySound { get; set; }

    /// <summary>
    /// Indicates if cross-platform notification dismissal synchronization hooks are enabled for this endpoint terminal state.
    /// </summary>
    [JsonPropertyName("dismissal_sync_enabled")]
    public bool DismissalSyncEnabled { get; set; }
}
