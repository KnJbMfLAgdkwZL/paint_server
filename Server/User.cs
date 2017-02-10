using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Newtonsoft.Json;
using System.Threading;
namespace Server
{
    class User
    {
        public Socket client = null;
        public String name = "";
        public String userinfo = "";
        public long lastmes = 0;
        public String roomid = "";
        public void SetName(String name)
        {
            this.name = name;
        }
        public void SetUserinfo(String userinfo)
        {
            this.userinfo = userinfo;
        }
        public void SetLastmes(long lastmes)
        {
            this.lastmes = lastmes;
        }
        public void SetRoom(String room)
        {
            this.roomid = room;
        }
        public String GetRoom()
        {
            return this.roomid;
        }
        public Socket GetClient()
        {
            return this.client;
        }
        public void SetClient(Socket client)
        {
            this.client = client;
        }
        public String GetName()
        {
            return this.name;
        }
        public String GetRoomid()
        {
            return this.roomid;
        }
        public long GetLastmes()
        {
            return this.lastmes;
        }
        public bool EqualsClient(Socket client)
        {
            return this.client.Equals(client);
        }
        public User(Socket client, String name, String userinfo, long lastmes, String roomid)
        {
            this.client = client;
            this.name = name;
            this.userinfo = userinfo;
            this.lastmes = lastmes;
            this.roomid = roomid;
        }
        public User()
        {
        }
        public String ToString()
        {
            String str = "";
            str += this.client.RemoteEndPoint;
            str += " " + name;
            str += " " + this.userinfo;
            return str;
        }
    }
}