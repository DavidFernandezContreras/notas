// Ejemplo: de la “tabla” Customers, ordena por LastName y devuelve los Emails
var q = DynamicEfQuery.Query(context, "Customers", "LastName", "Email", ascending: true);

// Materializar (si quieres)
var emails = await q.ToListAsync(); // List<object> con los valores de Email