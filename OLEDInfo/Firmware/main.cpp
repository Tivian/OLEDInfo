#include "i2c_master.hpp"
#include "uart.hpp"
#include <avr/interrupt.h>
#include <avr/wdt.h>
#include <util/delay.h>

int main(void) {
    UART::init(0, 7, true);
    I2C::init(1, 0);
    sei();

    UART::recv([](uint8_t data) {
        static volatile uint8_t n = 0;
        static volatile uint16_t i = 0, counter = 0;

        if (n == 0) {
            I2C::start(data, I2C::mode_t::write);
        } else if (n == 1) {
            counter = data;
            counter <<= 8;
        } else if (n == 2) {
            counter |= data;
            i = 0;
            wdt_enable(WDTO_120MS);
        } else if (n == 3) {
            I2C::write(data);
            wdt_reset();

            if (++i >= counter) {
                I2C::stop();
                wdt_disable();
                n = 0;
            }
            
            return;
        }

        n++;
    });

    for (;;) { }

    return 0;
}
