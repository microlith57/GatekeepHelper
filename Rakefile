require 'rake/clean'

CLEAN << 'release'
CLOBBER << 'GatekeepHelper.zip'

task :build do
  sh 'dotnet build -c Release'
end
file 'bin/Release/net452/GatekeepHelper.dll' => [:build]

directory 'release'
file 'release/everest.yaml' => %w[release everest.yaml] do
  sh <<~BASH
    sed "s/bin\\/Debug\\/net452\\/GatekeepHelper.dll/GatekeepHelper.dll/" everest.yaml > release/everest.yaml
  BASH
end
file 'release/Ahorn' => %w[release Ahorn] do
  cp_r 'Ahorn', 'release/'
end
file 'release/Graphics' => %w[release Graphics] do
  cp_r 'Graphics', 'release/'
end
file 'release/GatekeepHelper.dll' => %w[release bin/Release/net452/GatekeepHelper.dll] do
  cp 'bin/Release/net452/GatekeepHelper.dll', 'release/'
end

file 'GatekeepHelper.zip' => %w[release/everest.yaml release/GatekeepHelper.dll release/Ahorn release/Graphics] do
  cd 'release'
  sh 'zip -r9 ../GatekeepHelper.zip .'
end

task default: ['GatekeepHelper.zip', :clean]
