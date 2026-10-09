# 斜坡向右卡住：运行时对照

检查日期：2026-10-10。模组基线：Independent 1.1.19，macOS ARM64，tModLoader 2026.8.3.0 / Terraria 1.4.4.9。

## 已定位的触发路径

`RegionManager.Load` 挂钩 `Collision.TileCollision`。在游戏自带的 .NET 8.0.0 下，挂钩后角色向右走上右下三角斜砖会停在砖的左边缘；去掉这个钩子后正常。相同模组和测试地形在系统 .NET 8.0.25 下正常。

原测试在 40 帧内测得：

| 运行时 | 碰撞钩子 | 向右位移 | 向左位移 |
|---|---|---:|---:|
| .NET 8.0.0 | TPVPA 原钩子 | 40 px | 120 px |
| .NET 8.0.0 | 无钩子 | 120 px | 120 px |
| .NET 8.0.0 | 只调用原版并原样返回的 On 钩子 | 40 px | 120 px |
| .NET 8.0.25 | TPVPA 原钩子 | 120 px | 120 px |

这排除了“出生区禁止出入判断主动把速度清零”作为此场景的原因：没有区域判断的原样返回钩子也会出错。已定位到碰撞方法的运行时挂钩兼容路径，尚未继续定位 MonoMod 或 JIT 内部的具体错误。

按用户描述让脚底右端恰好接触斜砖左下角、从静止开始，40 帧的实测更加直接：.NET 8.0.0 原钩子向右位移 **0 px**，向左 **65.240234 px**；卸下钩子后左右均为 **65.240234 px**。原始测量保存在 `/tmp/pvpa-slope-contact-runtime8-server.log`，标记 `SLOPE_CONTACT runtime=8.0.0`。

尝试把 TileCollision 改为返回点的 IL 钩子，.NET 8.0.0 下仍然失败，因此已撤回，没有把它作为修复提交。原版半砖／斜坡算法没有修改，模组版本保持 1.1.19。

## 后续复核

`Tests/IndependentChecks/RuntimeChecks.cs` 的 `MeasureSlopes` 增加了脚底恰好接触斜砖左下角、从静止加速的镜像测量，以及 ±0.01 / ±0.5 像素偏移检查。另检查禁止进入、允许进入、左右禁止退出和允许退出的区域边界。

测试必须记录实际 `Environment.Version`。在同一个隔离目录中分别启动游戏自带的 `dotnet_arm64/dotnet` 与系统 `dotnet --fx-version 8.0.25`，才能保持模组和地形不变。此前只用系统运行时得到的通过结果，不能代表游戏自带运行时的行为。

当前可验证的规避方向是让客户端使用 .NET 8.0.25。此次没有修改游戏安装目录的运行时，也没有重启正在运行的客户端或远程服务器；尚未进行真实客户端画面的验收。

最终复核：同一份模组包在系统 .NET 8.0.25 下累计 **231 项检查通过**，包含精确接触点、五种亚像素偏移及区域出入限制。`SLOPE_CONTACT runtime=8.0.25` 记录模组开启／卸下钩子的左右位移均为 **65.240234 px**。日志保存在 `/tmp/pvpa-slope-runtime825-server.log`。

在本机 tModLoader 安装目录下，可用已安装的运行时启动客户端：

```sh
cd '/Users/lhy/Library/Application Support/Steam/steamapps/common/tModLoader'
DYLD_LIBRARY_PATH="$PWD/Libraries/Native/OSX" /usr/local/share/dotnet/dotnet --fx-version 8.0.25 tModLoader.dll -arm64 -gldevice:Vulkan
```

该命令未在此次检查中启动客户端；仅用于下一次启动时选择已验证的运行时，不会升级远程服务器的模组。
