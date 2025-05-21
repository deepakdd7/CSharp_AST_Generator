# CSharp_AST_Generator
<ol>
  <li> cd /root</li>
  <li> mkdir Indigo-AST</li>
  <li> cd Indigo-AST/</li>
  <li> dotnet new console -n ReturnDependencyAnalyzer</li>
  <li> cd ReturnDependencyAnalyzer/</li>
  <li> dotnet add package Microsoft.CodeAnalysis.CSharp</li>
  <li> dotnet add package Microsoft.CodeAnalysis.Common</li>
  <li> vi Program.cs</li>
  <li> dotnet build -c Release</li>
  <li> git clone https://github.com/anuraj/MinimalApi</li>
  <li> dotnet run --project ReturnDependencyAnalyzer/ReturnDependencyAnalyzer.csproj /root/Indigo-AST/MinimalApi</li>
  <li> cp /root/Indigo-AST/MinimalApi/metadata.json .
</ol>



