# Improving AppSettings Configuration Handling in .NET

(published in https://medium.com/@reyronald/improving-appsettings-configuration-handling-in-net-e854e6c15305)

When dealing with custom application settings in a .NET solution you basically have two choices: 1) use the Application Settings Architecture (https://docs.microsoft.com/en-us/dotnet/framework/winforms/advanced/application-settings-architecture), or 2) use the `<appSettings>` tag of your configuration file (https://msdn.microsoft.com/en-us/library/aa903313(v=vs.71).aspx). Each have their pros and cons that I won't detail here, but generally choice no. 1 is accepted as the superior one because of its support for type-safe access at compile-time. Here are some resources that talk about them and compare them in-depth:

- https://stackoverflow.com/questions/460935/pros-and-cons-of-appsettings-vs-applicationsettings-net-app-config-web-confi
- https://stackoverflow.com/questions/1058853/what-is-the-difference-between-the-applicationsettings-section-and-the-appsettin
- https://stackoverflow.com/questions/2350893/appsettings-vs-applicationsettings-appsettings-outdated
- https://stackoverflow.com/questions/1772140/using-app-config-to-set-strongly-typed-variables
- http://geekswithblogs.net/DougLampe/archive/2014/11/10/boo-appsettings--yay-applicationsettings.aspx

However, that doesn’t mean that the appSettings tag is completely useless. It still has its place and is widely used because of its simplicity and low overhead in comparison with the alternative. Also, the former has some limitations that the latter doesn’t. For instance, with the Application Settings Architecture is not possible to share configurations among several projects.

With a little bit of code, we can bring a high level of robustness to the appSettings mechanism, which in my opinion, may even make it superior in most cases. Here I will outline my approach to achieve this and more, without a lot of the noise and generated code Visual Studio produces with other solutions, which will leave us with a lot cleaner and less “magical” solution. Something to note is that this only applies as-is to .NET Framework, because configuration handling in .NET Core and .NET Standard is slightly different, although the concept itself can still be transferable.

### What we are aiming for

In a vanilla approach with appSettings we deal with configuration directly using the `ConfigurationManager` API, which works by giving the developer access to a dictionary of key/values that represent the settings included in the `app.config` or `web.config` file of the current project (a `NameValueCollection` to be exact). Although this works fine, it is a low-level API that if not wrapped or augmented with more code, can be the cause of many headaches in a development environment. Here are some of the weaknesses of this approach in general, and particular ones that we were facing with our product:

- Since the access to the configuration values in the code is done through a dictionary-like structure, there's no autocompletion, intellisense,  suggestions or tooling of any kind when using the store. This has several implications… for starters, it means that to access a value, you have to make sure what key is it associated to by looking into the configuration file (`app.config` or `web.config`). Secondly, if you mistyped the key when accessing it, you would have no compile time check of this mistake, and it would be a run-time error that could easily slip into production. 
- All the values in the `ConfigurationManager` dictionary return a string, which means that there has to be a manual casting or conversion of any non-string configuration value prior to its use in the code, introducing a lot of boilerplate code to the logic, and possible run-time errors when the value was not configured in the right format.
- Duplication! In a Service Oriented Architecture or any other setup where there are multiple Start-Up projects, each one of them needs to have their own configuration file, which in many cases will result in many duplicated configuration settings across the solution.
- An architecture which can help us avoid the Static Cling anti-pattern (see http://deviq.com/static-cling/).

The purpose of the proposed overhaul is to address these weaknesses and to make configuration handling much more friendlier for the developer. In contrast, the benefits that I am seeking to achieve are:

- Reduction of bugs and errors introduced by mistyped configuration values and/or with incorrect format.
- Elimination of the need to convert configuration values to a correct data type before each use.
- Ease to add automated tests to validate the presence, format and type of configuration values.
- Possibility to use IDE or automated tooling with configuration settings: search all references, renaming, warnings and suggestions, compilation errors, consolidation, etc.
- Reduction of human mistakes when manipulating or dealing with configuration.
- Reduction of number of configuration files to maintain and their size.
- Removal of duplicated configuration values across the whole solution.

The approach I suggest is fairly simple yet powerful, but requires a little bit of setup and considerations.

### The approach in a nutshell

We are going to need a new Class Library project solely to host the new necessary code, let's refer to it as the `AppConfig` project. This new project must be referenced by any other project that needs access to configuration values. The MVP here will be the following simple class:

```c#
using System.Configuration;
using System.Runtime.CompilerServices;

namespace AppConfig
{
  public class AppSettingsParent
  {
    protected AppSettingsParent() { }

    protected static string Get([CallerMemberName]string propertyName = null)
      => ConfigurationManager.AppSettings[propertyName];
  }
}
```

The idea is that every other project will create a subclass of this `AppSettingsParent` and include in it a static read-only property with the same name of the configuration key to be accessed, and then use this property instead of the direct call to `ConfigurationManager`. This will give us a pseudo type-safe mechanism to access the settings. I use the word pseudo here, because this might still fail at run-time if the name of the property doesn't match an existing key in the file. With this approach, we are using the `[CallerMemberName]` attribute to use the actual name of the property as the key that we are going to use to look-up the value in the `ConfigurationManager`. 

Here's a simplified example in action:

```c#
namespace AppConfig
{
  public sealed class CommonAppSettings : AppSettingsParent
  {
    private CommonAppSettings() { }
    
    public static string Title => Get();
    public static int Port => int.Parse(Get());
  }
}
```

This would correspond to a configuration file of the form:

```xml
<appSettings>
  <add key="Title" value="Configuration Mechanism Overhaul" />
  <add key="Port" value="3000" />
</appSettings>
```

Then, we would just use it like `CommonAppSettings.Port`, which would give us an already parsed to `int` access to the configuration value! This mimics what the Application Settings Architecture achieves with its Designer, with a lot less code, less overhead, more flexibility, and much more power and control to the developer.

Note that is not actually necessary to have this in another project, these classes could easily be located in your Start-Up project and that's it. However, in complex and large solutions you can benefit from separating it this way so that other projects can reuse not only the classes, but configuration too, more on this in the next section.

Something else that's beautiful about this, is that we can easily include unit tests that use reflection to iterate over all the static properties of our subclasses, and make sure no exception is thrown when they're accessed, or no default value is returned (if that makes sense to your case, of course). The caveat here though is that in our unit test project we must link and/or "mount" this configuration file, see the repo linked in the end for a suggestion on how to go about this.

### Reusing config values across projects

As usual, each Start-Up project will have its own configuration file, but how then do we deal with the issue of duplicated config values across these projects? As soon as you identify the same config key/value pair being used in more than one project, that is a candidate for factoring it out. Here's exactly how you do it:

1. Create a standalone configuration file with a non-default name, something like  `common.app.config` file in your `AppConfig` project.
2. Include in it the common configuration key/values that you identified.
3. Link to it from the start-up projects and set the "Copy to Output Directory" to "Copy if newer". You do this by right-clicking the project name in Solution Explorer, choosing "Add Existing File", browse to the `common.app.config` and make sure to check the "Add as Link" checkbox. This will add it to your project, but as a link, not a copy, meaning that every change made to the original file will be visible from your referencing projects. Then, under the solution explorer, right click the linked config file, Properties, and change there the copy settings. In your `.csproj` file you should see something similar to this added:
	
  ```xml
    <Content Include="..\AppConfig\common.app.config">
      <Link>common.app.config</Link>
     <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  ```
	
4. Use the `file` attribute of the actual `appSettings` tag of the current project to reference the linked common config file. You could reference the original file back in the `AppConfig` project too and it will work, choose whichever makes more sense to you, however, keep in mind that you still need to include the link to the file regardless because otherwise it won't be deployed when the application is published. Also, you will need to transform this configuration file for deployment and change the `file` attribute in that environment to point to the root instead.

  ```xml
	<!-- web.config in Web project -->
	<appSettings file=".\bin\common.app.config"> <!-- In Release this should be file="common.app.config" -->
		<!-- Other keys... -->
	</appSettings>
  ```
	
You are done! The same approach can be used with connection strings as well. For that case though, you would need to use the `configSource` attribute instead, see https://stackoverflow.com/questions/6940004/asp-net-web-config-configsource-vs-file-attributes/6940086#6940086.

### See it in action

I've set up a repo that demonstrate mostly everything I've explained so far and a little more, you can check the minimalist pull request https://github.com/reyronald/AppSettingsOverhaul/pull/2, or explore the entire source.
