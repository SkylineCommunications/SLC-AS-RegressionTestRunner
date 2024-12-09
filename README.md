# RegressionTestRunner

This script is part of the [Regression Tests](https://catalog.dataminer.services/catalog/4574) package and should be mainly used a part of it.

## Scheduled Regression Runs

Whenever it is possible, scheduled regression runs are typically a good idea by making use of the scheduled tasks in the Scheduler module. You can schedule your own automation scripts and that will work just fine.
When you are using the local application package, you can make use of this Automation Script that comes along with the package. There is one script parameter that you need to specify, i.e. 'ScriptConfiguration'. It is a JSON formatted string which give you the freedom to run all regression tests, available in the specified folder or given by name. There is also the possibility to specify recipients, which will receive an e-mail report with the tests that have run.

Example of the input:

```json
    {
        "Folders": ["RTManager/The Pioneers/RT_ThePioneers"],
        "Scripts": ["RT_1", "RT_2"],
        "SearchSubDirectories": True,
        "ScriptsToSkip": ["RT_3"],
        "FoldersToSkip": ["RTManager/The Pioneers/RT_ThePioneers/Workflows"],
        "Recipients": ["someone@somewhere.com"]
    }
```
