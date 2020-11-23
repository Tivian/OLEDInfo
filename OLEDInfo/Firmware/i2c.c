#include "i2c.h"

static uchar transfer(uchar data) {
    USISR = data;                               // Set USISR according to data.
                                                // Prepare clocking.
    data = 0<<USISIE | 0<<USIOIE |              // Interrupts disabled
           1<<USIWM1 | 0<<USIWM0 |              // Set USI in Two-wire mode.
           1<<USICS1 | 0<<USICS0 | 1<<USICLK |  // Software clock strobe as source.
           1<<USITC;                            // Toggle Clock Port.

    do {
        DELAY_T2TWI;
        USICR = data;                           // Generate positive SCL edge.
        loop_until_bit_is_set(PIN_USI_CL, PIN_USI_SCL); // Wait for SCL to go high.
        DELAY_T4TWI;
        USICR = data;                           // Generate negative SCL edge.
    } while (bit_is_clear(USISR, USIOIF));      // Check for transfer complete.

    DELAY_T2TWI;
    data = USIDR;                               // Read out data.
    USIDR = 0xFF;                               // Release SDA.
    DDR_USI |= _BV(PIN_USI_SDA);                // Enable SDA as output.

    return data;                                // Return the data from the USIDR
}

void i2c_init(void) {
    PORT_USI |= _BV(PIN_USI_SDA);               // Enable pullup on SDA.
    PORT_USI_CL |= _BV(PIN_USI_SCL);            // Enable pullup on SCL.

    DDR_USI_CL |= _BV(PIN_USI_SCL);             // Enable SCL as output.
    DDR_USI |= _BV(PIN_USI_SDA);                // Enable SDA as output.

    USIDR = 0xFF;                               // Preload data register with "released level" data.
    USICR = 0<<USISIE | 0<<USIOIE |             // Disable Interrupts.
            1<<USIWM1 | 0<<USIWM0 |             // Set USI in Two-wire mode.
            1<<USICS1 | 0<<USICS0 | 1<<USICLK | // Software stobe as counter clock source
            0<<USITC;
    USISR = 1<<USISIF | 1<<USIOIF | 1<<USIPF | 1<<USIDC | // Clear flags,
            0x0<<USICNT0;                       // and reset counter.
}

void i2c_start(void) {
    /* Release SCL to ensure that (repeated) Start can be performed */
    PORT_USI_CL |= _BV(PIN_USI_SCL);            // Release SCL.
    loop_until_bit_is_set(PIN_USI_CL, PIN_USI_SCL); // Verify that SCL becomes high.
#ifdef TWI_FAST_MODE
    DELAY_T4TWI;
#else
    DELAY_T2TWI;
#endif

    /* Generate Start Condition */
    PORT_USI &= ~_BV(PIN_USI_SDA);              // Force SDA LOW.
    DELAY_T4TWI;
    PORT_USI_CL &= ~_BV(PIN_USI_SCL);           // Pull SCL LOW.
    PORT_USI |= _BV(PIN_USI_SDA);               // Release SDA.
}

void i2c_repstart(void) {
    i2c_start();
}

void i2c_stop(void) {
    PORT_USI &= ~_BV(PIN_USI_SDA);              // Pull SDA low.
    PORT_USI_CL |= _BV(PIN_USI_SCL);            // Release SCL.
    loop_until_bit_is_set(PIN_USI_CL, PIN_USI_SCL); // Wait for SCL to go high.
    DELAY_T4TWI;
    PORT_USI |= _BV(PIN_USI_SDA);               // Release SDA.
    DELAY_T2TWI;
}

uchar i2c_put_u08(uchar data) {
    /* Write a byte */
    PORT_USI_CL &= ~_BV(PIN_USI_SCL);           // Pull SCL LOW.
    USIDR = data;                               // Setup data.
    transfer(USISR_8bit);                       // Send 8 bits on bus.

    /* Clock and verify (N)ACK from slave */
    DDR_USI &= ~_BV(PIN_USI_SDA);               // Enable SDA as input.
    return !(transfer(USISR_1bit) & _BV(TWI_NACK_BIT));
}

uchar i2c_get_u08(uchar last) {
    /* Read a byte */
    DDR_USI &= ~_BV(PIN_USI_SDA);               // Enable SDA as input.
    uchar data = transfer(USISR_8bit);

    /* Prepare to generate ACK (or NACK in case of End Of Transmission) */
    USIDR = last ? 0xFF : 0x00;
    transfer(USISR_1bit);                       // Generate ACK/NACK.

    return data;                                // Read successfully completed
}
