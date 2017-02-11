using System;
using Alchemy.Classes;
namespace PaintServer
{
    class User
    {
        public UserContext client = null;
        public String name = "";
        public String info = "";
        public String room = "";
        public int time = 0;
        public User(UserContext client, String name, String info, String room, int time)
        {
            this.client = client;
            this.name = name;
            this.info = info;
            this.room = room;
            this.time = time;
        }
    }
}