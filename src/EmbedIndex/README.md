# Sample Nuget Package Indexer #
  
This is a proof of concept implementation of creating a nuget package which contains the symbol_index.json necessary for the [Package Based Symbol Server](../../Documentation/Specs/Foo.md)  
  
## Running the example ##

###Create a hello world nuget package that we can use as input ###

Follow the directions to make a HelloWorld sample here: https://www.microsoft.com/net/core. After the sample runs use 'nuget pack' to create a nuget package

    C:\>mkdir hwapp
    C:\>cd hwapp
    C:\hwapp>dotnet new
    C:\hwapp>dotnet restore
    C:\hwapp>dotnet pack

### Build the EmbedIndex project in this directory ###

    C:\git\symstore\src\EmbedIndex>dotnet restore
    C:\git\symstore\src\EmbedIndex>dotnet build

### Run the EmbedIndex app

It uses arguments <path\_to\_existing\_package> <path\_to\_new\_indexed\_package>

    C:\git\symstore\src\EmbedIndex>dotnet run C:\hwapp\bin\Debug\hwapp.1.0.0.nupkg C:\hwapp\indexed\hwapp.1.0.0.nupkg
    
When you open the package at C:\hwapp\indexed you will see a symbol\_index.json has been added. You can open package using tools like package explorer or by changing the extension to .zip and using standard tools. The symbol\_index.json file looks like this:

    {
      "5784a7cb00008000" : "lib/netcoreapp1.0/hwapp.dll"
    }

## Further exploration ##

The embed index tool handles a variety of managed and native formats. Give it a try on other packages.

Check out the [SSQP key conventions](todo/add/this/spec) for more details about how the indexing keys are generated.