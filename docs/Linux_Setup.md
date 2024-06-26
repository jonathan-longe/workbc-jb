# Linux setup

Running Job Board locally using Docker Compose.

## Initial setup
- `cd src`
- `docker-compose -f docker-compose.yml -f docker-compose.linux-dev.yml up`
- `docker-compose -f docker-compose.yml -f docker-compose.linux-dev.yml exec mssql /opt/mssql-tools/bin/sqlcmd -U sa -P 18^fh4M08aB@`
```
create database WorkBC_JobBoard_DEV;
go
create login jobboard with password = 'password', default_database = WorkBC_JobBoard_DEV, check_expiration = off, check_policy = off;
use WorkBC_JobBoard_DEV;
create user jobboard for login jobboard;
alter role [db_owner] add member [jobboard];
go
```
- Stop the containers and restart to run the Job Board data migrations - allow 15 minutes running time for the `WorkBC_JobBoard_DEV` to be fully populated.
- `docker cp /path/to/WorkBC_Enterprise_DEV.bak src-mssql-1:/var/opt/mssql/data/WorkBC_Enterprise_DEV.bak`
- `docker-compose -f docker-compose.yml -f docker-compose.linux-dev.yml exec mssql /opt/mssql-tools/bin/sqlcmd -U sa -P 18^fh4M08aB@`
```
restore database [WorkBC_Enterprise_DEV] from disk = N'/var/opt/mssql/data/WorkBC_Enterprise_DEV.bak'
with replace, move 'WorkBCEnterprise' to '/var/opt/mssql/data/WorkBC_Enterprise_DEV.mdf',
move 'WorkBCEnterprise_log' to '/var/opt/mssql/data/WorkBC_Enterprise_DEV.ldf'
go
```
- Download SSOT dump at https://github.com/bcgov/workbc-ssot/blob/master/ssot-full.sql.gz
- `gunzip -k -c ssot-full.sql.gz | docker-compose -f docker-compose.yml -f docker-compose.linux-dev.yml exec -T postgres psql --username workbc ssot && docker-compose -f docker-compose.yml -f docker-compose.linux-dev.yml kill -s SIGUSR1 ssot`

## Troubleshooting

- While building, you get the error:
```
Status: pull access denied for src_jb-dotnet-build, repository does not exist or may require 'docker login': denied: requested access to the resource is denied, Code: 1
```
It should be enough to retry the same line to get past the error.
- While starting up, your MSSQL container crashes with:
```
Reason: 0x00000001
db-1  |          Signal: SIGABRT - Aborted (6)
```
You are running a [Linux kernel that's unsupported by MSSQL](https://github.com/microsoft/mssql-docker/issues/868). You will need to downgrade the Linux kernel to 6.6.
