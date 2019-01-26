using System;

namespace BifrostExtended
{
    public enum AuthState
    {
        Unauthenticated = 0,
        Authenticating,
        Authenticated,
    }

    public enum PrivilegeLevel
    {
        Guest = 0,
        User,
        Moderator,
        Administrator,
        Sysop
    }

    public class ClientData
    {
        public static int NextClientId = 0;

        public AuthState AuthenticationState = AuthState.Unauthenticated;
        public int ClientId;
        public KeyData ClientKeys = new KeyData();

        public string ClientName = "";
        public bool Connected;
        public UserConnection Connection;
        public PrivilegeLevel PrivilegeLevel = PrivilegeLevel.Guest;
        public string ClientGuid { get; internal set; }
        public DateTime TimeConnected { get; private set; }

        public ClientData(UserConnection userConnection)
        {
            this.Connection = userConnection;
            this.TimeConnected = DateTime.Now;
            this.Connected = true;
            ClientGuid = Guid.NewGuid().ToString();
            ClientId = NextClientId;
            NextClientId++;
        }
    }
}