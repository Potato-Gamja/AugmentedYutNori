using System;
using System.Collections.Generic;

// 증강 id를 실제 증강 객체 생성 함수와 연결하는 목록
public static class AugmentFactories
{
    public static Dictionary<string, Func<int, IAugment>> Create()
    {
        var augments = new Dictionary<string, Func<int, IAugment>>();

        augments.Add("Contrarian", team => new ContrarianAugment(team));
        augments.Add("Sprinter", team => new SprinterAugment(team));
        augments.Add("Monk", team => new MonkAugment(team));
        augments.Add("FriendShield", team => new FriendShieldAugment(team));
        augments.Add("Rethrow", team => new RethrowAugment(team));
        augments.Add("Pacifist", team => new PacifistAugment(team));
        augments.Add("Marathoner", team => new MarathonerAugment(team));
        augments.Add("Assassin", team => new AssassinAugment(team));
        augments.Add("Loner", team => new LonerAugment(team));
        return augments;
    }
}