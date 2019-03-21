This folder should contain the following assemblies, copied from $(VsInstallRoot)Common7\IDE\ReferenceAssemblies\Microsoft\Framework\MonoAndroid\v1.0:

- Mono.Android.dll
- mscorlib.dll
- System.dll

Without these files in this folder, tests will fail.

We need to figure out a way to bring those in without bloating the repository with binary files.
