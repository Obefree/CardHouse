using System;

namespace Cob.Data
{
    [Serializable]
    public sealed class CobCardRecord
    {
        public int card_id;
        public string name;
        public string type;
        public string color;
        public int cost;

        public string effect1;
        public string effect1text;
        public string effect2;
        public string effect2text;

        public int copies;
        public string image;
        public bool has_image;
        public string card_type;
        public string set;

        public string sprite_url;
        public int sprite_num_w;
        public int sprite_num_h;
    }
}

