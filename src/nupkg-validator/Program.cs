using Nullean.Argh;
using NupkgValidator;

var app = new ArghApp();
app.MapAndRootAlias<ValidatorCommand>();

return await app.RunAsync(args);
