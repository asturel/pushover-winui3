using System;
using System.Collections.Generic;
using System.IO;

namespace PushoverDesktopClient;

public interface IMessageStorage
{
    void SaveMessage(long id, string rawJson);
    List<PushoverMessageEventArgs> LoadAllMessages();
    void DeleteMessage(long id);
}
