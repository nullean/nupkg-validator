using Nullean.Argh;
using NupkgValidator;

var app = new ArghApp();
app.Map<ValidatorCommand>();

return await app.RunAsync(args);
