using System;
using Utils;

namespace Game
{
    [Serializable]
    public struct Player
    {
        public int clientID;
        public string name;
        public int hp;
        public Vec3 position;
    }
    
    public class PlayerController
    {
        
    }
}