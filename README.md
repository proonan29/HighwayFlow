# HighwayFlow

Highway traffic-flow simulator for Unity 6 (URP). IDM car-following + MOBIL lane changes, 2-5 lanes, five scenarios:
1 plain highway, 2 on-ramp, 3 exit then on-ramp, 4 on-ramp then exit, 5 exit ramp with a traffic light.

- Adjust vehicles per minute, ramp share, exit share, ramp spacing, signal cycle/green ratio, truck share.
- Efficiency: throughput, served ratio, mean speed, delay, stopped share, queues. A vehicles-per-minute sweep draws curves for all scenarios and writes Reports/sweep_*.csv.
- UI in Korean/English. Open Assets/HighwayFlow/Scenes/HighwayFlow.unity and press Play.
- Headless: AgentTools\hf.ps1 hf_sweep --lanes 4 --points 8

Tested with Unity 6000.6.3f1. The simulation core (Assets/HighwayFlow/Scripts/Runtime, except renderers/camera) is plain C#.
