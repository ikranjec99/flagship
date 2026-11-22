insert into environments (id, project_id, name, key)
select @Id, id, @Name, @Key
from projects
where id = @ProjectId;
