select id, email, password_hash
from admin_users
where email = @Email;
