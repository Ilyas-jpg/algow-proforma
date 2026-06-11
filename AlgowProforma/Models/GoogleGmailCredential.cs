using System;

namespace AlgowProforma.Models;

public class GoogleGmailCredential
{
    public string GoogleSub { get; set; } = "";
    public string GoogleEmail { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime TokenExpiresAt { get; set; }
    public string GrantedScopes { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public bool IsConnected =>
        !string.IsNullOrWhiteSpace(GoogleEmail)
        && !string.IsNullOrWhiteSpace(AccessToken)
        && !string.IsNullOrWhiteSpace(RefreshToken);

    public bool AccessTokenExpired =>
        string.IsNullOrWhiteSpace(AccessToken) || DateTime.Now.AddMinutes(2) >= TokenExpiresAt;
}
