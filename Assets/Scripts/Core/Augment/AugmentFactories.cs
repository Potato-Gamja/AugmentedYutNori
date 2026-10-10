using System;
using System.Collections.Generic;

// 증강 id를 실제 증강 객체 생성 함수와 연결하는 목록
public static class AugmentFactories
{
    public static Dictionary<string, Func<int, IAugment>> Create()
    {
        var augments = new Dictionary<string, Func<int, IAugment>>();

        augments.Add("BackDoSwitch", team => new BackDoSwitchAugment(team));
        augments.Add("QuickStart", team => new QuickStartAugment(team));
        augments.Add("WeakBoost", team => new WeakBoostAugment(team));
        augments.Add("SturdyCarry", team => new SturdyCarryAugment(team));
        augments.Add("Rethrow", team => new RethrowAugment(team));
        return augments;
    }
}