# Gitter Sql Scripts


## Selecting User and Roles

```sql
SELECT
	*
FROM dbo.users u
INNER JOIN dbo.user_roles ur
	ON ur.user_id = u.user_id
INNER JOIN dbo.roles r
	ON r.role_id = ur.role_id
WHERE email = 'morgan@gmail.com'
```
## Inserting Roles
```sql
INSERT INTO
	DBO.USER_ROLES (USER_ID, ROLE_ID)
VALUES
	(4, 1),
	(4, 2)
```