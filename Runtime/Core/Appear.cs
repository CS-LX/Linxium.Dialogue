using System.Collections.Generic;
using UnityEngine;


namespace Linxium.Dialogue {
    public struct Appear {
        public string character; //角色名称
        public Sprite tachie; //立绘
        public Sprite background; //背景

        public Appear(string character, Sprite tachie, Sprite background) {
            this.character = character;
            this.tachie = tachie;
            this.background = background;
        }

        public static Appear Create(List<string> inkTag, string resourcesRootPath = "Dialogues/") {
            string character = string.Empty;
            Sprite tachie = null;
            Sprite background = null;
            foreach (var tag in inkTag) {
                string[] parts = tag.Split(':');
                switch (parts[0].ToLower()) {
                    case "character": character = parts[1]; break;
                    case "tachie": tachie = Resources.Load<Sprite>(resourcesRootPath + parts[1]); break;
                    case "background": background = Resources.Load<Sprite>(resourcesRootPath + parts[1]); break;
                }
            }
            return new Appear(character, tachie, background);
        }
    }
}