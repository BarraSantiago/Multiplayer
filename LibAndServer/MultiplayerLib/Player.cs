namespace MultiplayerLib
{
    [Serializable]
    public class Player
    {
        public int clientID = -1;
        public string name;
        public int hp = 3;
        public Vec3 pos;
        
        public bool ReduceHealth(int damage)
        {
            hp -= damage;

            return hp > 0;
        }
    }
}