select id, name, position, serve_value
from targeting_rules
where configuration_id = @ConfigurationId
order by position;
