using System;
using System.Collections.Generic;
using System.IO;
using BlackHole.Core;
using BlackHole.Unity;
class Program
{
    static void Check(bool ok,string msg) { if (!ok) throw new Exception(msg); }
    static NodeTree Tree(bool reverse)
    {
        var nodes=new List<NodeDefinition>();
        foreach(var id in reverse ? new[]{"b","a"} : new[]{"a","b"})
            nodes.Add(new NodeDefinition(id,new[]{new NodeRankDefinition(1,10,Array.Empty<NodeEffect>())}));
        var graph=new NodeGraph(new List<string>{"a","b"},new HashSet<string>{"a","b"},new Dictionary<string,IReadOnlyList<string>>{{"a",Array.Empty<string>()},{"b",Array.Empty<string>()}});
        return new NodeTree(graph,nodes,new NodeContent(new List<UpgradeStatDefinition>(),nodes));
    }
    static void Main()
    {
        var growth=new HqGrowthDefinition(new long[]{10,20,30});
        foreach(bool reverse in new[]{false,true})
        {
            var scenario=new PlaytestScenario{name="recipe",autoBuyBudget=10,gold=7};
            var progress=new ProgressState();var errors=new List<string>();var warnings=new List<string>();
            Check(ScenarioRunner.TryApply(scenario,progress,Tree(reverse),growth,errors,warnings),"recipe valid");
            Check(progress.RankOf("a")==1 && progress.RankOf("b")==0,"stable equal-cost order");
            Check(progress.Gold==7,"leftover budget distinct from starting Gold");
            scenario.autoBuyBudget=0;scenario.nodes.Add(new PlaytestScenario.Node{nodeId="a",rank=1});
            var snapshot=new ProgressState();
            Check(ScenarioRunner.TryApply(scenario,snapshot,Tree(reverse),growth,errors,warnings) && snapshot.RankOf("a")==1,"snapshot reproduction");
            scenario.nodes[0].nodeId="missing";errors.Clear();
            Check(!ScenarioRunner.TryApply(scenario,new ProgressState(),Tree(reverse),growth,errors,warnings),"unknown node rejected");
            scenario.nodes[0].nodeId="a";scenario.nodes[0].rank=2;errors.Clear();
            Check(!ScenarioRunner.TryApply(scenario,new ProgressState(),Tree(reverse),growth,errors,warnings),"invalid rank rejected");
        }
        Check(PlaytestScenario.Parse("{\"name\":\"bad\",\"nodes\":[{\"nodeId\":\"a\",\"rank\":1},{\"nodeId\":\"a\",\"rank\":1}]}",out _) == null,"duplicate nodes rejected");
        foreach(string file in Directory.GetFiles("Assets/Playtest/Scenarios","*.json"))
            Check(PlaytestScenario.Parse(File.ReadAllText(file),out _)!=null,"sample parses: "+file);
        Console.WriteLine("PASS: all Core compiled, deterministic budget, snapshot, Gold, invalid node/rank, duplicates and sample JSON");
    }
}
