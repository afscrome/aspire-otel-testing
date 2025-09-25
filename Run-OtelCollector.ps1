# Using JoinPath as docker is picky about the directory seperator of the host path
$exportDir = Join-Path . TestResults otel
$configPath = Join-Path . otel-exporter.yaml

docker run --rm -it `
    -p 4317:4317 `
    -v ${exportDir}:/mnt/otel-export `
    -v ${configPath}:/mnt/otel-config.yaml `
    -e ASPIRE_ENDPOINT=http://host.docker.internal:14317 `
    otel/opentelemetry-collector-contrib:latest `
    --config /mnt/otel-config.yaml
    