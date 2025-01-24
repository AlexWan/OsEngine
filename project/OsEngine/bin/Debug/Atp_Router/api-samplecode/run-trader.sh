#!/bin/bash

check_failure() {
    if [ $? -ne 0 ]
    then
        echo "There's something wrong!!!";
        exit -1;
    fi
}

mkdir build
g++ -I../linux64 -L../linux64 trader/src/trader.cpp -lthosttraderapi -o build/trader.exe
check_failure
export LD_LIBRARY_PATH=`pwd`/../linux64
cd build
./trader.exe

