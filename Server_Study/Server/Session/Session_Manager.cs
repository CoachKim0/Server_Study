namespace Server_Study;

public class Session_Manager
{
    static Session_Manager _session = new Session_Manager();

    public static Session_Manager Instance
    {
        get { return _session; }
    }

    /// <summary>
    /// 세션발급용 id
    /// </summary>
    private int _sessionid = 0;

    Dictionary<int, ClientSession> dicSession = new Dictionary<int, ClientSession>();
    object locked = new object();

    public ClientSession Generate()
    {
        lock (locked)
        {
            int sessionid = ++_sessionid;
            ClientSession session = new ClientSession();
            session.SessionId = sessionid;
            dicSession.Add(sessionid, session);
            Console.WriteLine($"Connected : {session.SessionId}");
            return session;
        }
    }

    public ClientSession Find(int id)
    {
        lock (locked)
        {
            ClientSession session = null;
            dicSession.TryGetValue(id, out session);
            return session;
        }
    }

    public void Remove(ClientSession session)
    {
        lock (locked)
        {
            dicSession.Remove(session.SessionId);
        }
    }
    
}