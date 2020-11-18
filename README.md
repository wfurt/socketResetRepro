# socketResetRepro

run 
```
loop.sh  bin/Debug/*/linux-x64/publish/stream
```

or 
```
loop.sh strace -tt -f -s 100 -o trace.txt  -e trace=epoll_wait,socket,connect,listen,close,socket,getsockname,bind,fcntl,setsockopt,epoll_ctl,shutdown,sendmsg,recvmsg -e trace=network bin/Debug/*/linux-x64/publish/stream
```
