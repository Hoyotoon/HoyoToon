## CN

为了能够直接访问UnityEditor的Internal类型, 这里使用了内置的[友元程序集](https://learn.microsoft.com/zh-cn/dotnet/standard/assembly/friend). 通过将自己的代码封装在内置友元程序集中, 无需修改引擎或反射即可访问Internal类型. 详见以下文章:

https://qiita.com/mob-sakai/items/f3bbc0c45abc31ea7ac0

示例项目:

https://github.com/mob-sakai/MainWindowTitleModifierForUnity

LWGUI选择将这部分代码封装在`Unity.InternalAPIEditorBridge.020`中, 但是如果你的项目已经定义了此程序集, 那么就会产生编译错误.

要解决这个错误, 你可以选择修改LWGUI或你工程中的同名Assembly Definition Asset, 以下两种方法都可以:

- 将其中一个Assembly Definition Asset删除, 并创建引用向另一个Assembly Definition Asset的[Assembly Definition Reference Asset](https://docs.unity3d.com/Manual/class-AssemblyDefinitionReferenceImporter.html)
- 将其中一个Assembly Definition Asset的名称修改为你的工程还未使用的程序集名称. 如`Unity.InternalAPIEditorBridge.021`, 所有可用的名称可以在[这里](https://github.com/Unity-Technologies/UnityCsReference/blob/1d7b2b49b93ea5773aa4e8dfa504e3c1533ce282/Editor/Mono/AssemblyInfo/AssemblyInfo.cs#L113)查询.



## EN

In order to directly access UnityEditor's Internal type, the built-in [Friend Assembly](https://learn.microsoft.com/zh-cn/dotnet/standard/assembly/friend) is used here. By encapsulating your own code In built-in friend assemblies, Internal types can be accessed without modifying the engine or Reflection. See the following articles for details:

https://qiita.com/mob-sakai/items/f3bbc0c45abc31ea7ac0

Example project:

https://github.com/mob-sakai/MainWindowTitleModifierForUnity

LWGUI chooses to encapsulate this part of the code in `Unity.InternalAPIEditorBridge.020`, but if your project has already defined this assembly, a compilation error will occur.

To solve this error, you can choose to modify the LWGUI or the Assembly Definition Asset with the same name in your project. Both of the following methods are available:

- Delete one Assembly Definition Asset and create an [Assembly Definition Reference Asset](https://docs.unity3d.com/Manual/class-AssemblyDefinitionReferenceImporter.html) that references another Assembly Definition Asset.
- Change the name of one of the Assembly Definition Assets to an assembly name not yet used by your project. For example, `Unity.InternalAPIEditorBridge.021`, all available names can be found [here](https://github.com/Unity-Technologies/UnityCsReference/blob/1d7b2b49b93ea5773aa4e8dfa504e3c1533ce282/Editor/Mono/AssemblyInfo/AssemblyInfo.cs#L113) query.
