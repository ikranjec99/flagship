insert into feature_flags (id, project_id, name, key, description)
select @Id, id, @Name, @Key, @Description
from projects
where id = @ProjectId;
